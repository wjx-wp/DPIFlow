using DPIFlow.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DPIFlow.UI
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly List<MonitorInfo> _monitors;
        private readonly Func<WindowInfo> _lastWindow;
        private readonly Action<AppSettings> _save;
        private readonly BindingList<ApplicationRule> _rules;
        private readonly DataGridView _grid = new DataGridView();
        private readonly CheckBox _enabled = new CheckBox();
        private readonly CheckBox _startup = new CheckBox();

        internal SettingsForm(AppSettings settings, List<MonitorInfo> monitors, Func<WindowInfo> lastWindow, Action<AppSettings> save)
        {
            _settings = settings; _monitors = monitors; _lastWindow = lastWindow; _save = save; _rules = new BindingList<ApplicationRule>(settings.Rules.ToList());
            Text = "DPIFlow Settings"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(860, 520); Size = new Size(980, 620);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 5, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 125)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _enabled.Text = "Enable automation"; _enabled.Checked = _settings.Enabled; _startup.Text = "Start with Windows"; _startup.Checked = _settings.StartWithWindows;
            top.Controls.Add(_enabled); top.Controls.Add(_startup); top.Controls.Add(new Label { AutoSize = true, Text = "  Windows handles normal Per-Monitor DPI; rules are only for apps that need extra per-display behavior." });
            root.Controls.Add(top, 0, 0);

            var monitorList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            monitorList.Columns.Add("Display", 110); monitorList.Columns.Add("Name", 180); monitorList.Columns.Add("Match key", 130); monitorList.Columns.Add("DPI / scale", 110); monitorList.Columns.Add("Bounds", 220);
            foreach (var m in _monitors) monitorList.Items.Add(new ListViewItem(new[] { m.DeviceName + (m.Primary ? " *" : ""), string.IsNullOrWhiteSpace(m.FriendlyName) ? "(unnamed)" : m.FriendlyName, m.MatchKey, m.DpiX + " / " + m.ScalePercent + "%", m.Bounds.ToString() }));
            root.Controls.Add(monitorList, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var quick = new Button { Text = "Quick-add last app", AutoSize = true }; quick.Click += (s, e) => QuickAdd();
            var blank = new Button { Text = "Add blank rule", AutoSize = true }; blank.Click += (s, e) => _rules.Add(new ApplicationRule());
            var remove = new Button { Text = "Remove selected", AutoSize = true }; remove.Click += (s, e) => RemoveSelected();
            buttons.Controls.Add(quick); buttons.Controls.Add(blank); buttons.Controls.Add(remove);
            root.Controls.Add(buttons, 0, 2);

            ConfigureGrid(); root.Controls.Add(_grid, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var save = new Button { Text = "Save && Close", AutoSize = true }; save.Click += (s, e) => SaveAndClose();
            var cancel = new Button { Text = "Cancel", AutoSize = true }; cancel.Click += (s, e) => Close();
            footer.Controls.Add(save); footer.Controls.Add(cancel); root.Controls.Add(footer, 0, 4);
            Controls.Add(root);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill; _grid.AutoGenerateColumns = false; _grid.AllowUserToAddRows = false; _grid.DataSource = _rules; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "On", DataPropertyName = "Enabled", FillWeight = 35 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Process", DataPropertyName = "ProcessName", FillWeight = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Monitor contains", DataPropertyName = "MonitorMatch", FillWeight = 100 });
            var adapter = new DataGridViewComboBoxColumn { HeaderText = "Adapter", DataPropertyName = "Adapter", FillWeight = 110 };
            adapter.Items.AddRange("Observe", "ChromiumKeyboardZoom"); _grid.Columns.Add(adapter);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target %", DataPropertyName = "TargetPercent", FillWeight = 55 });
        }

        private void QuickAdd()
        {
            var window = _lastWindow();
            if (window == null) { MessageBox.Show("Use the target application, then open DPIFlow settings from the tray. DPIFlow remembers the last foreground app.", "DPIFlow"); return; }
            bool chromium = string.Equals(window.ProcessName, "chrome.exe", StringComparison.OrdinalIgnoreCase) || string.Equals(window.ProcessName, "msedge.exe", StringComparison.OrdinalIgnoreCase);
            uint minDpi = _monitors.Count == 0 ? 96 : _monitors.Min(m => m.DpiX == 0 ? 96 : m.DpiX);
            foreach (var monitor in _monitors)
            {
                if (_rules.Any(r => string.Equals(r.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase) && string.Equals(r.MonitorMatch, monitor.MatchKey, StringComparison.OrdinalIgnoreCase))) continue;
                int target = chromium && minDpi > 0 && monitor.DpiX / (double)minDpi >= 1.25 ? 90 : 100;
                _rules.Add(new ApplicationRule { ProcessName = window.ProcessName, MonitorMatch = monitor.MatchKey, Adapter = chromium ? "ChromiumKeyboardZoom" : "Observe", TargetPercent = target });
            }
        }

        private void RemoveSelected()
        {
            var rows = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ApplicationRule).Where(r => r != null).ToList();
            foreach (var rule in rows) _rules.Remove(rule);
        }
        private void SaveAndClose()
        {
            _grid.EndEdit(); _settings.Enabled = _enabled.Checked; _settings.StartWithWindows = _startup.Checked; _settings.Rules = _rules.ToList(); _save(_settings); DialogResult = DialogResult.OK; Close();
        }
    }
}
