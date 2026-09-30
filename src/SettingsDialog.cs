using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace VoK.ReactionTimer;

internal sealed class SettingsDialog : Form
{
    private readonly TimerEngine _engine;
    private readonly Settings _settings;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly ComboBox _reaction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly NumericUpDown _warning = new() { Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = .5m, Width = 65 };
    private readonly NumericUpDown _alertSeconds = new() { Name = "RespikeAlertSeconds", Minimum = 0, Maximum = 60, Width = 65 };
    private readonly TextBox _size = new() { Width = 55, MaxLength = 3 };
    private readonly CheckBox _sound = new() { Text = "Sound when spike ends", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(4) };

    public SettingsDialog(TimerEngine engine)
    {
        _engine = engine; _settings = engine.GetSettings();
        Text = "Reaction Timer — settings and effects";
        Size = new Size(760, 540); MinimumSize = new Size(650, 460);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var controls = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        _warning.Value = (decimal)_settings.WarningSeconds; _size.Text = _settings.FontSizePixels.ToString(CultureInfo.InvariantCulture);
        _sound.Checked = _settings.SoundOnExpiry;
        controls.Controls.AddRange(new Control[] { new Label { Text = "Warn at (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _warning,
            new Label { Text = "Font size (px)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _size, _sound });
        layout.Controls.Add(controls, 0, 0);
        _alertSeconds.Value = _settings.RespikeAlertSeconds;
        var alertControls = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        alertControls.Controls.AddRange(new Control[] {
            new Label { Text = "Re-spike alert (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _alertSeconds,
            new Label { Text = "2× text + flashing black background (0 = off)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) } });
        layout.Controls.Add(alertControls, 0, 1);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Automatic detection looks for Reaction Spike names. If needed, trigger a spike, click Refresh, select its temporary effect and assign a reaction. Recent effects remain listed for 60 seconds." }, 0, 2);
        _grid.Columns.Add("did", "Effect DID"); _grid.Columns[0].FillWeight = 23;
        _grid.Columns.Add("name", "Name"); _grid.Columns[1].FillWeight = 70;
        _grid.Columns.Add("duration", "Duration"); _grid.Columns[2].FillWeight = 20;
        _grid.Columns.Add("age", "Last seen"); _grid.Columns[3].FillWeight = 23;
        _grid.Columns.Add("mapping", "Override"); _grid.Columns[4].FillWeight = 24;
        layout.Controls.Add(_grid, 0, 3);
        foreach (var r in Enum.GetValues<Reaction>()) _reaction.Items.Add(r);
        _reaction.SelectedItem = Reaction.Pyrite;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        var refresh = new Button { Text = "Refresh", AutoSize = true };
        var assign = new Button { Text = "Track selected", AutoSize = true };
        var remove = new Button { Text = "Clear override", AutoSize = true };
        var export = new Button { Text = "Export diagnostics", AutoSize = true };
        refresh.Click += (_, _) => Populate();
        assign.Click += (_, _) => SetMapping(true);
        remove.Click += (_, _) => SetMapping(false);
        export.Click += (_, _) => Export();
        actions.Controls.AddRange(new Control[] { refresh, _reaction, assign, remove, export });
        layout.Controls.Add(actions, 0, 4); layout.Controls.Add(_status, 0, 5);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            try
            {
                if (!SettingsInput.TryFontSize(_size.Text, out int pixels))
                { _status.Text = "Font size must be a whole number from 10 to 160 pixels. No changes saved."; _size.Focus(); return; }
                _settings.WarningSeconds = (double)_warning.Value; _settings.FontSizePixels = pixels;
                _settings.RespikeAlertSeconds = (int)_alertSeconds.Value;
                // Position/enabled state may have changed through the HUD controls while this dialog was open.
                var current = _engine.GetSettings(); _settings.HudX = current.HudX; _settings.HudY = current.HudY; _settings.HudEnabled = current.HudEnabled;
                _settings.SoundOnExpiry = _sound.Checked; _engine.SaveSettings(_settings); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception e) { _status.Text = "Could not save settings: " + e.Message; }
        };
        footer.Controls.AddRange(new Control[] { save, cancel }); layout.Controls.Add(footer, 0, 6);
        CancelButton = cancel; AcceptButton = save; Controls.Add(layout); Populate();
    }
    private void Populate()
    {
        int? selected = _grid.SelectedRows.Count > 0 ? (int?)_grid.SelectedRows[0].Tag : null;
        _grid.Rows.Clear();
        var effects = _engine.GetEffects().ToDictionary(x => x.Did);
        foreach (var id in _settings.EffectMappings.Keys)
            if (!effects.ContainsKey(id)) effects[id] = new(id, "(saved mapping)", null, 0);
        foreach (var effect in effects.Values.OrderByDescending(e => ReactionMatcher.Match(e.Name, e.Duration).HasValue).ThenBy(e => e.Name))
        {
            double age = TimerEngine.Now - effect.LastSeen;
            int row = _grid.Rows.Add(effect.Did, string.IsNullOrWhiteSpace(effect.Name) ? "(name unavailable)" : effect.Name,
                effect.Duration?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?",
                effect.LastSeen == 0 ? "Not active" : age < 2 ? "Now" : $"{age:0}s ago",
                _settings.EffectMappings.TryGetValue(effect.Did, out var mapped) ? mapped.ToString() : "Auto");
            _grid.Rows[row].Tag = effect.Did;
            if (selected == effect.Did) { _grid.ClearSelection(); _grid.Rows[row].Selected = true; }
        }
        _status.Text = "Select the temporary spike, not the permanent base reaction. Overrides take effect after Save.";
    }
    private void SetMapping(bool assign)
    {
        if (_grid.SelectedRows.Count == 0) { _status.Text = "Select an effect first."; return; }
        int did = (int)_grid.SelectedRows[0].Tag!;
        if (assign)
        {
            var effect = _engine.GetEffects().FirstOrDefault(e => e.Did == did);
            if (effect?.Duration is <= 0) { _status.Text = "This appears to be a permanent effect. Select the temporary Reaction Spike instead."; return; }
            _settings.EffectMappings[did] = (Reaction)_reaction.SelectedItem!;
        }
        else _settings.EffectMappings.Remove(did);
        Populate();
    }
    private void Export()
    {
        try
        {
            string path = Path.Combine(_engine.Folder, "reaction-timer-effects.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new { PluginVersion = typeof(ReactionTimerPlugin).Assembly.GetName().Version!.ToString(), Effects = _engine.GetEffects(),
                Settings = _settings }, new JsonSerializerOptions { WriteIndented = true }));
            _status.Text = "Saved: " + path;
        }
        catch (Exception e) { _status.Text = "Export failed: " + e.Message; }
    }
}
