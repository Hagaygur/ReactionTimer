using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VoK.Sdk.Plugins;

namespace VoK.ReactionTimer;

// Dungeon Helper hosts the controls only. The HUD has its own top-level window.
internal sealed class ReactionTimerUi : IPluginUI, IDisposable
{
    private readonly ReactionTimerPlugin _plugin;
    private ControlForm? _form;
    private Bitmap? _icon;
    public ReactionTimerUi(ReactionTimerPlugin plugin) => _plugin = plugin;
    public float? FocusedOpacity => 1f;
    public bool EnabledInCharacterSelection => true;
    public Tuple<int, int> MinSize => Tuple.Create(_form?.MinimumSize.Width ?? 360, _form?.MinimumSize.Height ?? 170);
    public object UserInterfaceForm => _form is null || _form.IsDisposed ? _form = new ControlForm(_plugin) : _form;
    public Image ToolbarImage
    {
        get
        {
            if (_icon is not null) return _icon;
            _icon = new Bitmap(36, 36);
            using var g = Graphics.FromImage(_icon);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(255, 181, 67), 3);
            g.DrawEllipse(pen, 5, 6, 26, 26);
            g.DrawLine(pen, 18, 11, 18, 20); g.DrawLine(pen, 18, 20, 24, 23);
            g.DrawLine(pen, 13, 2, 23, 2);
            return _icon;
        }
    }
    public void Dispose()
    {
        var form = _form; _form = null;
        if (form is not null && !form.IsDisposed)
        {
            if (form.IsHandleCreated && form.InvokeRequired)
            { try { form.BeginInvoke(new Action(form.Dispose)); } catch (InvalidOperationException) { } }
            else form.Dispose();
        }
        // ToolbarImage remains valid through host teardown; Bitmap releases its
        // native handle when the host drops this UI object.
    }
}

internal sealed class ControlForm : Form
{
    private readonly ReactionTimerPlugin _plugin;
    private readonly TableLayoutPanel _layout;
    private readonly TableLayoutPanel _buttons;
    private readonly Button _preview = new() { Text = "Preview", AutoSize = true };
    private readonly Button _move = new() { Text = "Move (20s)", AutoSize = true };
    private readonly Button _toggle = new() { Text = "Hide timer", AutoSize = true };
    private readonly Button _settings = new() { Text = "Settings / effects", AutoSize = true };
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill };
    private readonly Label _legend = new() { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 0, 3, 4),
        Text = "▲ PYR → Ceruleite   ◆ ORC → Gildleaf   ✚ VRD → Crimsonite" };
    private readonly Label _hint = new() { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 0, 3, 2),
        Text = "Close this panel while playing; timer stays on." };
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 500 };
    private UiTheme? _appliedTheme;

    public ControlForm(ReactionTimerPlugin plugin)
    {
        _plugin = plugin;
        Text = "Reaction Timer controls"; FormBorderStyle = FormBorderStyle.None;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(360, 195); MinimumSize = new Size(360, 170);
        AutoScroll = true;

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(6)
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 4; row++) _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(4)
        };
        _buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _preview.Click += (_, _) => _plugin.Hud?.Preview();
        _move.Click += (_, _) => _plugin.Hud?.Move();
        _toggle.Click += (_, _) =>
        {
            try
            {
                var engine = _plugin.Engine; if (engine is null) return;
                var s = engine.GetSettings(); s.HudEnabled = !s.HudEnabled;
                engine.SaveSettings(s); RefreshStatus();
            }
            catch (Exception e) { _status.Text = "Could not save: " + e.Message; }
        };
        _settings.Click += (_, _) =>
        {
            if (_plugin.Engine is not { } engine) return;
            using var dialog = new SettingsDialog(engine);
            dialog.ShowDialog(this);
            RefreshStatus();
        };

        foreach (var button in new[] { _preview, _move, _toggle, _settings }) button.Dock = DockStyle.Fill;
        _buttons.Controls.Add(_preview, 0, 0); _buttons.Controls.Add(_move, 1, 0);
        _buttons.Controls.Add(_toggle, 0, 1); _buttons.Controls.Add(_settings, 1, 1);
        _status.AutoSize = true; _status.Margin = new Padding(3, 8, 3, 4);
        _layout.Controls.Add(_buttons, 0, 0);
        _layout.Controls.Add(_status, 0, 1);
        _layout.Controls.Add(_legend, 0, 2);
        _layout.Controls.Add(_hint, 0, 3);
        Controls.Add(_layout);

        ApplyTheme(UiTheme.EberronGold);
        _refresh.Tick += (_, _) => RefreshStatus();
        VisibleChanged += (_, _) =>
        {
            _refresh.Enabled = Visible;
            if (Visible) { FitToolbarWidth(); RefreshStatus(); }
        };
        RefreshStatus();
    }

    private void ApplyTheme(UiTheme theme)
    {
        if (_appliedTheme == theme) return;
        _appliedTheme = theme;
        var p = UiThemePalettes.Get(theme);
        Color canvas = ToColor(p.SurfaceLowest);
        Color card = ToColor(p.SurfaceLow);
        Color btnBg = ToColor(p.Surface);
        Color btnHover = ToColor(p.SurfaceHigh);
        Color outline = ToColor(p.Outline);
        Color primary = ToColor(p.Primary);
        Color text = ToColor(p.OnSurface);
        Color muted = ToColor(p.OnSurfaceVariant);

        BackColor = canvas; ForeColor = text;
        _layout.BackColor = canvas;
        _buttons.BackColor = card;
        _status.ForeColor = text;
        _legend.ForeColor = primary;
        _hint.ForeColor = muted;
        StyleButton(_preview, btnBg, btnHover, outline, text);
        StyleButton(_move, btnBg, btnHover, outline, text);
        StyleButton(_toggle, btnBg, btnHover, outline, text);
        StyleButton(_settings, btnBg, btnHover, primary, text);
    }

    private static void StyleButton(Button button, Color bg, Color hover, Color border, Color text)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = bg;
        button.ForeColor = text;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.Margin = new Padding(3);
    }

    private static Color ToColor(ThemeRgb c) => Color.FromArgb(c.R, c.G, c.B);

    // DH calls Show() before using this form's Size to resize its host. Its
    // toolbar can be wider than the plugin panel, so preserve that width before
    // the host measures us. Use public control geometry, not host private fields.
    private void FitToolbarWidth()
    {
        foreach (Form host in Application.OpenForms)
        {
            if (host == this || host.IsDisposed || host.InvokeRequired) continue;
            var icon = FindNamedControl(host, _plugin.Name);
            if (icon?.Parent is not { } strip || !strip.IsHandleCreated) continue;
            int right = host.PointToClient(strip.PointToScreen(new Point(strip.Width, 0))).X;
            int allowance = (int)Math.Ceiling(36 * host.DeviceDpi / 96.0);
            int required = Math.Max(MinimumSize.Width, right + allowance);
            MinimumSize = new Size(required, MinimumSize.Height);
            Width = Math.Max(Width, required);
            break;
        }
    }
    private static Control? FindNamedControl(Control root, string name)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Name == name) return child;
            if (FindNamedControl(child, name) is { } found) return found;
        }
        return null;
    }
    private void RefreshStatus()
    {
        var s = _plugin.Engine?.GetSettings();
        if (s is not null) ApplyTheme(s.Theme);
        _status.Text = _plugin.Hud?.Status ?? "Waiting for game connection";
        _toggle.Text = s?.HudEnabled == false ? "Show timer" : "Hide timer";
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _refresh.Stop(); _refresh.Dispose(); }
        base.Dispose(disposing);
    }
}
