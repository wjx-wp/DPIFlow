using DPIFlow.Models;
using DPIFlow.Native;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DPIFlow.Services
{
    internal static class LogService
    {
        private static readonly object Sync = new object();
        internal static string FolderPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DPIFlow"); } }
        internal static string FilePath { get { return Path.Combine(FolderPath, "dpiflow.log"); } }

        internal static void Info(string message) { Write("INFO", message); }
        internal static void Error(string message, Exception ex = null) { Write("ERROR", message + (ex == null ? string.Empty : " | " + ex)); }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(FolderPath);
                    if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024)
                    {
                        string oldPath = FilePath + ".old";
                        if (File.Exists(oldPath)) File.Delete(oldPath);
                        File.Move(FilePath, oldPath);
                    }
                    File.AppendAllText(FilePath, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}{3}", DateTime.Now, level, message, Environment.NewLine));
                }
            }
            catch { }
        }
    }

    internal sealed class MonitorService
    {
        private readonly object _sync = new object();
        private List<MonitorInfo> _cache = new List<MonitorInfo>();

        internal MonitorService()
        {
            Refresh();
        }

        internal void Refresh()
        {
            var next = EnumerateMonitors();
            lock (_sync) _cache = next;
            LogService.Info("Monitor cache refreshed. Count=" + next.Count);
        }

        internal List<MonitorInfo> GetMonitors()
        {
            lock (_sync) return _cache.ToList();
        }

        internal MonitorInfo GetForWindow(IntPtr hwnd)
        {
            IntPtr handle = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (handle == IntPtr.Zero) return null;

            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(mi);
            if (!NativeMethods.GetMonitorInfo(handle, ref mi)) return null;

            lock (_sync)
            {
                return _cache.FirstOrDefault(m => string.Equals(m.DeviceName, mi.szDevice, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static List<MonitorInfo> EnumerateMonitors()
        {
            var list = new List<MonitorInfo>();
            foreach (var screen in Screen.AllScreens)
            {
                var dd = new NativeMethods.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(dd);
                NativeMethods.EnumDisplayDevices(screen.DeviceName, 0, ref dd, 0);

                var center = new NativeMethods.POINT(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
                IntPtr handle = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
                int scale = 100;
                try
                {
                    int hr = NativeMethods.GetScaleFactorForMonitor(handle, out scale);
                    if (hr != 0 || scale <= 0) scale = 100;
                }
                catch { scale = 100; }

                uint dpi = (uint)Math.Round(96.0 * scale / 100.0);
                list.Add(new MonitorInfo
                {
                    DeviceName = screen.DeviceName,
                    FriendlyName = dd.DeviceString,
                    DeviceId = dd.DeviceID,
                    Primary = screen.Primary,
                    Bounds = screen.Bounds,
                    DpiX = dpi,
                    DpiY = dpi
                });
            }
            return list;
        }
    }

    internal sealed class SettingsStore
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        internal string FolderPath { get { return LogService.FolderPath; } }
        internal string FilePath { get { return Path.Combine(FolderPath, "settings.json"); } }

        internal AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                var value = _json.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (value == null) return new AppSettings();
                if (value.Rules == null) value.Rules = new List<ApplicationRule>();
                value.Rules = value.Rules.Where(r => r != null).ToList();
                return value;
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to load settings; using defaults.", ex);
                return new AppSettings();
            }
        }

        internal void Save(AppSettings settings)
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, _json.Serialize(settings));
        }
    }

    internal static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (enabled) key.SetValue("DPIFlow", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("DPIFlow", false);
            }
        }
    }

    internal sealed class WindowInspector
    {
        private readonly MonitorService _monitors;
        internal WindowInspector(MonitorService monitors) { _monitors = monitors; }

        internal WindowInfo Inspect(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd)) return null;
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return null;

            try
            {
                using (var process = Process.GetProcessById((int)pid))
                {
                    return new WindowInfo
                    {
                        Hwnd = hwnd,
                        ProcessId = (int)pid,
                        ProcessName = process.ProcessName + ".exe",
                        Title = NativeMethods.ReadWindowTitle(hwnd),
                        Monitor = _monitors.GetForWindow(hwnd)
                    };
                }
            }
            catch { return null; }
        }
    }

    internal sealed class WindowChangedEventArgs : EventArgs
    {
        internal IntPtr Hwnd { get; private set; }
        internal uint EventType { get; private set; }
        internal WindowChangedEventArgs(IntPtr hwnd, uint eventType) { Hwnd = hwnd; EventType = eventType; }
    }

    internal sealed class WindowWatcher : IDisposable
    {
        private NativeMethods.WinEventDelegate _callback;
        private IntPtr _foregroundHook;
        private IntPtr _moveEndHook;
        internal event EventHandler<WindowChangedEventArgs> WindowChanged;

        internal void Start()
        {
            _callback = OnWinEvent;
            uint flags = NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS;
            _foregroundHook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback, 0, 0, flags);
            _moveEndHook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _callback, 0, 0, flags);
            if (_foregroundHook == IntPtr.Zero || _moveEndHook == IntPtr.Zero) throw new InvalidOperationException("Unable to install WinEvent hooks.");
        }

        private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint threadId, uint time)
        {
            var handler = WindowChanged;
            if (handler != null && hwnd != IntPtr.Zero) handler(this, new WindowChangedEventArgs(hwnd, eventType));
        }

        public void Dispose()
        {
            if (_foregroundHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_foregroundHook);
            if (_moveEndHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_moveEndHook);
            _foregroundHook = _moveEndHook = IntPtr.Zero;
        }
    }
}
