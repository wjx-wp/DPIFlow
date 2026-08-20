using System;
using System.Collections.Generic;
using System.Drawing;

namespace DPIFlow.Models
{
    public sealed class MonitorInfo
    {
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public string DeviceId { get; set; }
        public bool Primary { get; set; }
        public Rectangle Bounds { get; set; }
        public uint DpiX { get; set; }
        public uint DpiY { get; set; }
        public int ScalePercent { get { return DpiX == 0 ? 100 : (int)Math.Round(DpiX / 96.0 * 100.0); } }
        public string MatchKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DeviceId))
                {
                    var parts = DeviceId.Split('\\');
                    if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) return parts[1];
                }
                return DeviceName ?? string.Empty;
            }
        }
        public override string ToString() { return string.Format("{0} ({1}, {2}%)", string.IsNullOrWhiteSpace(FriendlyName) ? DeviceName : FriendlyName, MatchKey, ScalePercent); }
    }

    public sealed class WindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string Title { get; set; }
        public MonitorInfo Monitor { get; set; }
    }

    public sealed class ApplicationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public string ProcessName { get; set; } = "chrome.exe";
        public string MonitorMatch { get; set; } = string.Empty;
        public string Adapter { get; set; } = "Observe";
        public int TargetPercent { get; set; } = 100;

        public bool Matches(WindowInfo window)
        {
            if (!Enabled || window == null || window.Monitor == null) return false;
            if (!string.Equals(NormalizeExe(ProcessName), NormalizeExe(window.ProcessName), StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(MonitorMatch)) return true;
            string needle = MonitorMatch.Trim();
            return Contains(window.Monitor.DeviceId, needle) || Contains(window.Monitor.DeviceName, needle) || Contains(window.Monitor.FriendlyName, needle) || Contains(window.Monitor.MatchKey, needle);
        }

        private static string NormalizeExe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = value.Trim();
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value : value + ".exe";
        }
        private static bool Contains(string value, string needle) { return !string.IsNullOrEmpty(value) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0; }
    }

    public sealed class AppSettings
    {
        public bool Enabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public int DebounceMs { get; set; } = 200;
        public List<ApplicationRule> Rules { get; set; } = new List<ApplicationRule>();
    }
}
