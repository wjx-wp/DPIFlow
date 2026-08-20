using DPIFlow.Adapters;
using DPIFlow.Models;
using DPIFlow.Native;
using DPIFlow.Services;
using DPIFlow.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DPIFlow
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly MonitorService _monitors = new MonitorService();
        private readonly SettingsStore _store = new SettingsStore();
        private readonly WindowWatcher _watcher = new WindowWatcher();
        private readonly WindowInspector _inspector;
        private readonly Dictionary<IntPtr, string> _lastApplied = new Dictionary<IntPtr, string>();
        private AppSettings _settings;
        private WindowInfo _lastObserved;
        private ToolStripMenuItem _enabledMenu;

        internal TrayApplicationContext()
        {
            _settings = _store.Load();
            _inspector = new WindowInspector(_monitors);
            LogService.Info("DPIFlow 0.1.0 starting. Rules=" + _settings.Rules.Count);
            try { StartupService.Apply(_settings.StartWithWindows); } catch (Exception ex) { LogService.Error("Failed to update startup registration.", ex); }

            _enabledMenu = new ToolStripMenuItem("Automation enabled") { Checked = _settings.Enabled, CheckOnClick = true };
            _enabledMenu.CheckedChanged += (s, e) => { _settings.Enabled = _enabledMenu.Checked; SaveSettings(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add(_enabledMenu);
            menu.Items.Add("Settings...", null, (s, e) => OpenSettings());
            menu.Items.Add("Diagnostics", null, (s, e) => ShowDiagnostics());
            menu.Items.Add("Open data folder", null, (s, e) => OpenDataFolder());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "DPIFlow", Visible = true, ContextMenuStrip = menu };
            _tray.DoubleClick += (s, e) => OpenSettings();

            _watcher.WindowChanged += OnWindowChanged;
            try { _watcher.Start(); }
            catch (Exception ex)
            {
                LogService.Error("Window monitoring failed to start.", ex);
                MessageBox.Show("DPIFlow could not start window monitoring.\n\n" + ex.Message, "DPIFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            if (_settings.Rules.Count == 0)
            {
                _tray.BalloonTipTitle = "DPIFlow is running";
                _tray.BalloonTipText = "No app rules yet. Use an app, then open Settings and choose Quick-add last app.";
                _tray.ShowBalloonTip(4000);
            }
        }

        private void OnWindowChanged(object sender, WindowChangedEventArgs e)
        {
            var window = _inspector.Inspect(e.Hwnd);
            if (window == null || window.Monitor == null) return;
            if (string.Equals(window.ProcessName, "DPIFlow.exe", StringComparison.OrdinalIgnoreCase)) return;
            if (IsUsefulLastApp(window)) _lastObserved = window;
            if (!_settings.Enabled) return;
            var rule = _settings.Rules.FirstOrDefault(r => r.Matches(window));
            if (rule == null) return;
            string signature = rule.Id + "|" + window.Monitor.MatchKey + "|" + rule.Adapter + "|" + rule.TargetPercent;
            string previous;
            bool force = e.EventType == NativeMethods.EVENT_SYSTEM_FOREGROUND;
            if (!force && _lastApplied.TryGetValue(window.Hwnd, out previous) && previous == signature) return;
            var result = AdapterFactory.Create(rule.Adapter).Apply(window, rule);
            if (result.Success)
            {
                _lastApplied[window.Hwnd] = signature;
                LogService.Info(string.Format("Applied {0} to {1} on {2}: {3}", rule.Adapter, window.ProcessName, window.Monitor.MatchKey, result.Message));
            }
            else if (!result.Deferred) LogService.Error(string.Format("Adapter {0} failed for {1} on {2}: {3}", rule.Adapter, window.ProcessName, window.Monitor.MatchKey, result.Message));
            SetTrayText(window.ProcessName + " · " + window.Monitor.MatchKey + " · " + (result.Message ?? ""));
        }

        private static bool IsUsefulLastApp(WindowInfo window)
        {
            if (window == null) return false;
            if (string.Equals(window.ProcessName, "explorer.exe", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(window.Title)) return false;
            if (string.Equals(window.ProcessName, "ShellExperienceHost.exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(window.ProcessName, "StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(window.ProcessName, "SearchUI.exe", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            _lastApplied.Clear();
            LogService.Info("Display configuration changed.");
            SetTrayText("Display configuration changed");
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_settings, _monitors.GetMonitors(), () => _lastObserved, SaveFromForm)) form.ShowDialog();
        }

        private void SaveFromForm(AppSettings settings)
        {
            _settings = settings;
            _enabledMenu.Checked = settings.Enabled;
            _lastApplied.Clear();
            SaveSettings();
            try { StartupService.Apply(settings.StartWithWindows); } catch (Exception ex) { LogService.Error("Failed to update startup registration.", ex); }
            LogService.Info("Settings saved. Rules=" + settings.Rules.Count);
        }

        private void SaveSettings()
        {
            try { _store.Save(_settings); }
            catch (Exception ex) { LogService.Error("Failed to save settings.", ex); }
        }

        private void OpenDataFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(_store.FolderPath);
                System.Diagnostics.Process.Start("explorer.exe", _store.FolderPath);
            }
            catch (Exception ex) { LogService.Error("Failed to open data folder.", ex); }
        }

        private void ShowDiagnostics()
        {
            var lines = new List<string> { "DPIFlow 0.1.0", "", "Monitors:" };
            lines.AddRange(_monitors.GetMonitors().Select(m => "- " + m));
            lines.Add("\nRules: " + _settings.Rules.Count);
            lines.Add("Settings: " + _store.FilePath);
            lines.Add("Log: " + LogService.FilePath);
            if (_lastObserved != null) lines.Add("\nLast app: " + _lastObserved.ProcessName + " @ " + _lastObserved.Monitor.MatchKey);
            MessageBox.Show(string.Join(Environment.NewLine, lines), "DPIFlow diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetTrayText(string value)
        {
            if (string.IsNullOrEmpty(value)) value = "DPIFlow";
            _tray.Text = value.Length > 63 ? value.Substring(0, 63) : value;
        }

        protected override void ExitThreadCore()
        {
            LogService.Info("DPIFlow exiting.");
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _watcher.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }
    }
}
