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
            try { StartupService.Apply(_settings.StartWithWindows); } catch { }

            _enabledMenu = new ToolStripMenuItem("Automation enabled") { Checked = _settings.Enabled, CheckOnClick = true };
            _enabledMenu.CheckedChanged += (s, e) => { _settings.Enabled = _enabledMenu.Checked; SaveSettings(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add(_enabledMenu);
            menu.Items.Add("Settings...", null, (s, e) => OpenSettings());
            menu.Items.Add("Diagnostics", null, (s, e) => ShowDiagnostics());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "DPIFlow", Visible = true, ContextMenuStrip = menu };
            _tray.DoubleClick += (s, e) => OpenSettings();

            _watcher.WindowChanged += OnWindowChanged;
            try { _watcher.Start(); }
            catch (Exception ex) { MessageBox.Show("DPIFlow could not start window monitoring.\n\n" + ex.Message, "DPIFlow", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void OnWindowChanged(object sender, WindowChangedEventArgs e)
        {
            var window = _inspector.Inspect(e.Hwnd);
            if (window == null || window.Monitor == null) return;
            if (string.Equals(window.ProcessName, "DPIFlow.exe", StringComparison.OrdinalIgnoreCase)) return;
            _lastObserved = window;
            if (!_settings.Enabled) return;
            var rule = _settings.Rules.FirstOrDefault(r => r.Matches(window));
            if (rule == null) return;
            string signature = rule.Id + "|" + window.Monitor.MatchKey + "|" + rule.Adapter + "|" + rule.TargetPercent;
            string previous;
            bool force = e.EventType == NativeMethods.EVENT_SYSTEM_FOREGROUND;
            if (!force && _lastApplied.TryGetValue(window.Hwnd, out previous) && previous == signature) return;
            var result = AdapterFactory.Create(rule.Adapter).Apply(window, rule);
            if (result.Success) _lastApplied[window.Hwnd] = signature;
            SetTrayText(window.ProcessName + " · " + window.Monitor.MatchKey + " · " + (result.Message ?? ""));
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e) { _lastApplied.Clear(); SetTrayText("Display configuration changed"); }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_settings, _monitors.GetMonitors(), () => _lastObserved, SaveFromForm)) form.ShowDialog();
        }

        private void SaveFromForm(AppSettings settings)
        {
            _settings = settings; _enabledMenu.Checked = settings.Enabled; _lastApplied.Clear(); SaveSettings();
            try { StartupService.Apply(settings.StartWithWindows); } catch { }
        }
        private void SaveSettings() { try { _store.Save(_settings); } catch { } }

        private void ShowDiagnostics()
        {
            var lines = new List<string> { "DPIFlow 0.1.0", "", "Monitors:" };
            lines.AddRange(_monitors.GetMonitors().Select(m => "- " + m));
            lines.Add("\nRules: " + _settings.Rules.Count);
            lines.Add("Settings: " + _store.FilePath);
            if (_lastObserved != null) lines.Add("\nLast window: " + _lastObserved.ProcessName + " @ " + _lastObserved.Monitor.MatchKey);
            MessageBox.Show(string.Join(Environment.NewLine, lines), "DPIFlow diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetTrayText(string value)
        {
            if (string.IsNullOrEmpty(value)) value = "DPIFlow";
            _tray.Text = value.Length > 63 ? value.Substring(0, 63) : value;
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _watcher.Dispose(); _tray.Visible = false; _tray.Dispose(); base.ExitThreadCore();
        }
    }
}
