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
    private readonly TableLayoutPanel _layout;
    private readonly FlowLayoutPanel _controlsRow;
    private readonly FlowLayoutPanel _alertRow;
    private readonly FlowLayoutPanel _gameplayRow;
    private readonly FlowLayoutPanel _actionsRow;
    private readonly FlowLayoutPanel _footerRow;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false };
    private readonly ComboBox _reaction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ComboBox _theme = new() { Name = "Theme", DropDownStyle = ComboBoxStyle.DropDownList, Width = 195 };
    private readonly NumericUpDown _warning = new() { Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = .5m, Width = 65 };
    private readonly NumericUpDown _alertSeconds = new() { Name = "RespikeAlertSeconds", Minimum = 0, Maximum = 60, Width = 65 };
    private readonly TextBox _size = new() { Width = 55, MaxLength = 3 };
    private readonly CheckBox _sound = new() { Text = "Sound when spike ends", AutoSize = true };
    private readonly CheckBox _suppressTown = new() { Name = "SuppressAlertsInTown", Text = "Suppress alerts in town / shrines", AutoSize = true };
    private readonly CheckBox _depletionBar = new() { Name = "ShowDepletionBar", Text = "Show depletion bar on HUD", AutoSize = true };
    private readonly Label _helpLabel = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 4, 2, 0),
        Text = "Automatic detection looks for Reaction Spike names (▲ Pyrite → Ceruleite, ◆ Orchidium → Gildleaf, ✚ Verdanite → Crimsonite). Trigger a spike, click Refresh, select its temporary effect and assign a reaction if needed." };
    private readonly Label _alertHint = new() { Text = "2× text + flashing black background (0 = off)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(4) };
    private readonly Button _refreshBtn = new() { Text = "Refresh", AutoSize = true };
    private readonly Button _assignBtn = new() { Text = "Track selected", AutoSize = true };
    private readonly Button _removeBtn = new() { Text = "Clear override", AutoSize = true };
    private readonly Button _exportBtn = new() { Text = "Export diagnostics", AutoSize = true };
    private readonly Button _saveBtn = new() { Text = "Save", AutoSize = true };
    private readonly Button _cancelBtn = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
    private readonly Font _dialogFont = new("Segoe UI", 9);

    public SettingsDialog(TimerEngine engine)
    {
        _engine = engine; _settings = engine.GetSettings();
        Text = "Reaction Timer — settings and effects";
        Size = new Size(780, 580); MinimumSize = new Size(680, 520);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = _dialogFont;

        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(10) };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _controlsRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
        _warning.Value = (decimal)_settings.WarningSeconds;
        _size.Text = _settings.FontSizePixels.ToString(CultureInfo.InvariantCulture);
        _sound.Checked = _settings.SoundOnExpiry;
        _controlsRow.Controls.AddRange(new Control[] {
            new Label { Text = "Warn at (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _warning,
            new Label { Text = "Font size (px)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _size, _sound });
        _layout.Controls.Add(_controlsRow, 0, 0);

        _alertSeconds.Value = _settings.RespikeAlertSeconds;
        _alertRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
        _alertRow.Controls.AddRange(new Control[] {
            new Label { Text = "Re-spike alert (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _alertSeconds, _alertHint });
        _layout.Controls.Add(_alertRow, 0, 1);

        _suppressTown.Checked = _settings.SuppressAlertsInTown;
        _depletionBar.Checked = _settings.ShowDepletionBar;
        foreach (var t in Enum.GetValues<UiTheme>()) _theme.Items.Add(UiThemePalettes.DisplayName(t));
        _theme.SelectedIndex = (int)_settings.Theme;
        _theme.SelectedIndexChanged += (_, _) => ApplyTheme((UiTheme)Math.Max(0, _theme.SelectedIndex));

        _gameplayRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
        _gameplayRow.Controls.AddRange(new Control[] {
            _suppressTown, _depletionBar,
            new Label { Text = "Theme", AutoSize = true, Padding = new Padding(10, 5, 0, 0) }, _theme });
        _layout.Controls.Add(_gameplayRow, 0, 2);

        _layout.Controls.Add(_helpLabel, 0, 3);

        _grid.Columns.Add("did", "Effect DID"); _grid.Columns[0].FillWeight = 23;
        _grid.Columns.Add("name", "Name"); _grid.Columns[1].FillWeight = 70;
        _grid.Columns.Add("duration", "Duration"); _grid.Columns[2].FillWeight = 20;
        _grid.Columns.Add("age", "Last seen"); _grid.Columns[3].FillWeight = 23;
        _grid.Columns.Add("mapping", "Override"); _grid.Columns[4].FillWeight = 24;
        _layout.Controls.Add(_grid, 0, 4);

        foreach (var r in Enum.GetValues<Reaction>()) _reaction.Items.Add(r);
        _reaction.SelectedItem = Reaction.Pyrite;
        _actionsRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        _refreshBtn.Click += (_, _) => Populate();
        _assignBtn.Click += (_, _) => SetMapping(true);
        _removeBtn.Click += (_, _) => SetMapping(false);
        _exportBtn.Click += (_, _) => Export();
        _actionsRow.Controls.AddRange(new Control[] { _refreshBtn, _reaction, _assignBtn, _removeBtn, _exportBtn });
        _layout.Controls.Add(_actionsRow, 0, 5);
        _layout.Controls.Add(_status, 0, 6);

        _footerRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        _saveBtn.Click += (_, _) =>
        {
            try
            {
                if (!SettingsInput.TryFontSize(_size.Text, out int pixels))
                { _status.Text = "Font size must be a whole number from 10 to 160 pixels. No changes saved."; _size.Focus(); return; }
                _settings.WarningSeconds = (double)_warning.Value; _settings.FontSizePixels = pixels;
                _settings.RespikeAlertSeconds = (int)_alertSeconds.Value;
                _settings.SuppressAlertsInTown = _suppressTown.Checked;
                _settings.ShowDepletionBar = _depletionBar.Checked;
                _settings.Theme = (UiTheme)Math.Max(0, _theme.SelectedIndex);
                // Position/enabled state may have changed through the HUD controls while this dialog was open.
                var current = _engine.GetSettings(); _settings.HudX = current.HudX; _settings.HudY = current.HudY; _settings.HudEnabled = current.HudEnabled;
                _settings.SoundOnExpiry = _sound.Checked; _engine.SaveSettings(_settings); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception e) { _status.Text = "Could not save settings: " + e.Message; }
        };
        _footerRow.Controls.AddRange(new Control[] { _saveBtn, _cancelBtn }); _layout.Controls.Add(_footerRow, 0, 7);
        CancelButton = _cancelBtn; AcceptButton = _saveBtn; Controls.Add(_layout);
        ApplyTheme(_settings.Theme);
        Populate();
    }

    private void ApplyTheme(UiTheme theme)
    {
        var p = UiThemePalettes.Get(theme);
        Color canvas = ToColor(p.SurfaceLowest);
        Color card = ToColor(p.SurfaceLow);
        Color input = ToColor(p.Surface);
        Color hover = ToColor(p.SurfaceHigh);
        Color outline = ToColor(p.Outline);
        Color primary = ToColor(p.Primary);
        Color text = ToColor(p.OnSurface);
        Color muted = ToColor(p.OnSurfaceVariant);

        BackColor = canvas; ForeColor = text;
        _layout.BackColor = canvas;
        _controlsRow.BackColor = card;
        _alertRow.BackColor = card;
        _gameplayRow.BackColor = card;
        _actionsRow.BackColor = canvas;
        _footerRow.BackColor = canvas;

        foreach (Control row in new Control[] { _controlsRow, _alertRow, _gameplayRow })
            foreach (Control c in row.Controls)
                if (c is Label or CheckBox) c.ForeColor = text;

        _alertHint.ForeColor = muted;
        _helpLabel.ForeColor = muted;
        _status.ForeColor = primary;

        foreach (Control c in new Control[] { _warning, _size, _alertSeconds, _reaction, _theme })
        {
            c.BackColor = input;
            c.ForeColor = text;
        }
        foreach (var b in new[] { _refreshBtn, _assignBtn, _removeBtn, _exportBtn, _cancelBtn })
            StyleButton(b, input, hover, outline, text);
        StyleButton(_saveBtn, input, hover, primary, text);

        _grid.BackgroundColor = card;
        _grid.GridColor = outline;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = input;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = primary;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = input;
        _grid.DefaultCellStyle.BackColor = card;
        _grid.DefaultCellStyle.ForeColor = text;
        _grid.DefaultCellStyle.SelectionBackColor = hover;
        _grid.DefaultCellStyle.SelectionForeColor = text;
    }

    private static void StyleButton(Button button, Color bg, Color hover, Color border, Color text)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = bg;
        button.ForeColor = text;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.Padding = new Padding(6, 2, 6, 2);
    }

    private static Color ToColor(ThemeRgb c) => Color.FromArgb(c.R, c.G, c.B);

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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dialogFont.Dispose();
        base.Dispose(disposing);
    }
}
