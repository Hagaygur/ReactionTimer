using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace VoK.ReactionTimer;

internal sealed class Dh5ReactionTimerUi
{
    private const string ControlWindowTitle = "Alch Reaction Timer###" + Dh5Plugin.PluginGuid;
    private static readonly Vector2 DefaultControlWindowSize = new(560f, 470f);
    private static readonly string[] DigitStrings = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static readonly Dictionary<(uint FontId, float Density, int FontPx), TabularMetrics> s_tabularCache = new();

    internal sealed class TabularMetrics
    {
        public float DigitCellW { get; }
        public float[] DigitWidths { get; }
        public float DotW { get; }
        public float TildeW { get; }
        public float LineH { get; }
        private readonly Dictionary<string, float> _prefixWidths = new(StringComparer.Ordinal);

        public TabularMetrics(float digitCellW, float[] digitWidths, float dotW, float tildeW, float lineH)
        {
            DigitCellW = digitCellW;
            DigitWidths = digitWidths;
            DotW = dotW;
            TildeW = tildeW;
            LineH = lineH;
        }

        public float MeasureCountdownWidth(double remaining, bool estimated)
        {
            if (!TrySplitCountdown(remaining, out string intPart, out string fracPart))
                return 0f;
            int intCols = Math.Max(2, intPart.Length);
            return (estimated ? TildeW : 0f) + intCols * DigitCellW + DotW + fracPart.Length * DigitCellW;
        }

        public float GetPrefixWidth(string prefix, Func<string, Vector2> measure)
        {
            if (_prefixWidths.TryGetValue(prefix, out float cached))
                return cached;
            float w = measure(prefix).X;
            _prefixWidths[prefix] = w;
            return w;
        }
    }

    // Semantic CVD-safe reaction & alert accents (constant across all 3 DDO x M3 themes so alerts always pop)
    private static readonly Vector4 AstralBlue = Hex(0x4D, 0xA6, 0xFF, 1.00f);
    private static readonly Vector4 PyriteCol = Hex(0xFF, 0xB5, 0x43, 1.00f);
    private static readonly Vector4 OrchidiumCol = Hex(0xBD, 0x91, 0xFF, 1.00f);
    private static readonly Vector4 VerdaniteCol = Hex(0x67, 0xDE, 0x9F, 1.00f);
    private static readonly Vector4 GenericCol = Hex(0xB4, 0xD0, 0xE6, 1.00f);
    private static readonly Vector4 CrimsonCol = Hex(0xFF, 0x4D, 0x5A, 1.00f);
    private static readonly Vector4 FlashGoldCol = Hex(0xFF, 0xD6, 0x44, 1.00f);

    private static readonly string[] HudModeLabels = { "Compact Pill + Depletion Bar", "Single-Arc Radial Donut" };
    private static readonly string[] ThemeLabels = Enum.GetValues<UiTheme>().Select(UiThemePalettes.DisplayName).ToArray();
    private static readonly string[] ReactionNames = Enum.GetNames<Reaction>();

    private readonly Dh5TimerEngine _engine;
    private readonly HudAlertState _liveAlert = new();
    private HudAlertState _previewAlert = new();

    private double _previewStart = double.NegativeInfinity;
    private double _previewDuration = 15;
    private double _editUntil = double.NegativeInfinity;
    private double _lastInteractiveSave = double.NegativeInfinity;
    private bool _interactiveDirty;
    private bool _resetWindowPending;
    private Settings _cachedSettings;
    private int _cachedRevision = -1;

    private int _selectedEffectDid;
    private int _selectedReactionIndex = (int)Reaction.Pyrite;
    private string _statusMessage = "";
    private bool _resizingHud;
    private float _resizeStartMouseY;
    private int _resizeStartFont;

    public Dh5ReactionTimerUi(Dh5TimerEngine engine)
    {
        _engine = engine;
        _cachedSettings = engine.GetSettings();
        _cachedRevision = engine.SettingsRevision;
    }

    internal void ResetLayout()
    {
        _editUntil = double.NegativeInfinity;
        _previewStart = double.NegativeInfinity;
        _interactiveDirty = false;
        _resetWindowPending = true;
        _statusMessage = "HUD position, size, and opacity reset.";
    }

    internal void StartPreview()
    {
        double now = Dh5TimerEngine.Now;
        _previewStart = now;
        _previewAlert = new();
        _previewDuration = 12 + Math.Max(3, _engine.GetSettings().RespikeAlertSeconds);
    }

    internal void StartEditMode()
    {
        _editUntil = Dh5TimerEngine.Now + 20.0;
    }

    public void Render(Dh5Plugin plugin)
    {
        double now = Dh5TimerEngine.Now;
        SyncSettings(now);
        var snap = _engine.Snapshot();
        var palette = UiThemePalettes.Get(_cachedSettings.Theme);
        RenderOverlayHud(snap, palette, now);
        if (plugin.WindowVisible)
            RenderControlWindow(plugin, snap, palette, now);
    }

    private void SyncSettings(double now)
    {
        int rev = _engine.SettingsRevision;
        if (rev != _cachedRevision)
        {
            // External updates (/alch commands, ResetUi, etc.) always win over pending drag debounce.
            _cachedSettings = _engine.GetSettings();
            _cachedRevision = rev;
            _interactiveDirty = false;
        }
        if (_interactiveDirty && now - _lastInteractiveSave > 0.35)
        {
            _interactiveDirty = false;
            try
            {
                _engine.SaveSettings(_cachedSettings);
                _cachedRevision = _engine.SettingsRevision;
            }
            catch (Exception e)
            {
                _statusMessage = "Could not save HUD adjustment: " + e.Message;
            }
        }
    }

    private void RenderOverlayHud(Dh5HudSnapshot snap, UiThemePalette palette, double now)
    {
        var s = _cachedSettings;
        bool demo = now - _previewStart < _previewDuration;
        bool editing = now < _editUntil;
        var live = snap.View;

        // Always advance live alert state so old expiries never replay after hiding/previewing.
        var liveAlert = _liveAlert.Update(live, now, s.RespikeAlertSeconds);

        bool display = (s.HudEnabled || demo || editing) &&
            (demo || editing || live.Mode != TimerMode.Disconnected);
        if (!display) return;

        var view = demo
            ? new TimerView(now - _previewStart < 12 ? TimerMode.Active : TimerMode.Expired,
                Reaction.Pyrite, Math.Max(0, 12 - (now - _previewStart)), 12, false, 0, "Preview")
            { BaseStance = Reaction.Pyrite }
            : live;

        var alert = demo ? _previewAlert.Update(view, now, s.RespikeAlertSeconds) : liveAlert;

        var vp = ImGui.GetMainViewport();
        Vector2 vpPos = vp.Pos;
        Vector2 vpSize = vp.Size;
        if (vpSize.X < 64 || vpSize.Y < 64) return;

        bool townPaused = !demo && view.InTown && view.Mode == TimerMode.Paused;
        bool warning = !townPaused && view.Mode == TimerMode.Active && view.Remaining <= s.WarningSeconds;
        bool expired = !townPaused && view.Mode == TimerMode.Expired;

        // Calm-to-urgent <= 2 Hz lifecycle (WCAG 2.3.1 compliant)
        float pulse = alert.Enlarged
            ? 0.5f + 0.5f * (float)Math.Sin(now * Math.PI * 3.6)   // 1.8 Hz bounded Re-Spike pulse
            : warning
                ? 0.5f + 0.5f * (float)Math.Sin(now * Math.PI * 2.4) // 1.2 Hz gentle pre-expiry breath
                : 0f;

        Vector4 accent = townPaused
            ? ToVec4(palette.OnSurfaceVariant)
            : (expired || warning)
                ? CrimsonCol
                : ReactionColor(view.Reaction);

        float alpha = (float)Math.Clamp(s.HudOpacity, 0.25, 1.0);
        int fontPx = alert.FontSize(s.FontSizePixels);
        string text = HudPresentation.Text(view, demo);
        string? primerHint = HudPresentation.NextPrimerHint(view);
        bool showSp = s.ShowSpellPointsBar && snap.SpellPointsMax is > 0 && snap.SpellPointsCurrent.HasValue;
        float spFraction = showSp
            ? Math.Clamp((float)snap.SpellPointsCurrent!.Value / Math.Max(1, snap.SpellPointsMax!.Value), 0f, 1f)
            : 0f;

        Vector2 hudSize = s.HudMode == HudDisplayMode.RadialDonut
            ? MeasureRadialDonut(fontPx, primerHint, showSp, editing)
            : MeasureCompactPill(fontPx, text, view, demo, s, townPaused, primerHint, showSp, editing);

        float posX = vpPos.X + (float)Math.Clamp(s.HudX, 0, 1) * Math.Max(0, vpSize.X - hudSize.X);
        float posY = vpPos.Y + (float)Math.Clamp(s.HudY, 0, 1) * Math.Max(0, vpSize.Y - hudSize.Y);
        Vector2 hudMin = new(MathF.Round(posX), MathF.Round(posY));

        var dl = ImGui.GetForegroundDrawList();
        if (s.HudMode == HudDisplayMode.RadialDonut)
            DrawRadialDonut(dl, hudMin, hudSize, fontPx, view, s, palette, alert, accent, alpha, pulse, townPaused, primerHint, showSp, spFraction, editing);
        else
            DrawCompactPill(dl, hudMin, hudSize, fontPx, text, view, demo, s, palette, alert, accent, alpha, pulse, townPaused, primerHint, showSp, spFraction, editing);

        if (editing)
        {
            HandleInteractiveHud(hudMin, hudSize, vpPos, vpSize, palette, now);
        }
        else if (expired && s.QuickRespikeEnabled && s.QuickRespikeBar >= 1 && s.QuickRespikeSlot >= 1 && !demo)
        {
            RenderQuickRespikePill(hudMin, hudSize, vpPos, vpSize, palette);
        }
    }

    private static Vector2 CalcTextSizeAt(ImFontPtr font, float pixelSize, string text)
    {
        ImGui.PushFont(font, pixelSize);
        try { return ImGui.CalcTextSize(text); }
        finally { ImGui.PopFont(); }
    }

    internal static bool IsTabularCountdown(TimerView view, bool townPaused) =>
        !townPaused && view.Mode == TimerMode.Active && view.Remaining.HasValue &&
        double.IsFinite(view.Remaining.Value) && view.Remaining.Value >= 0;

    internal static string CountdownPrefix(TimerView view, bool demo) =>
        (demo ? "PREVIEW" : view.Reaction == Reaction.Any ? "SPIKE" : view.Reaction.ToString().ToUpperInvariant()) + "  ";

    internal static bool TrySplitCountdown(double remaining, out string intPart, out string fracPart)
    {
        if (!double.IsFinite(remaining) || remaining < 0)
        {
            intPart = "";
            fracPart = "";
            return false;
        }
        string formatted = remaining.ToString("0.0", CultureInfo.InvariantCulture);
        int dotIdx = formatted.IndexOf('.');
        intPart = dotIdx >= 0 ? formatted[..dotIdx] : formatted;
        fracPart = dotIdx >= 0 ? formatted[(dotIdx + 1)..] : "0";
        return true;
    }

    internal static TabularMetrics MeasureTabularMetrics(Func<string, Vector2> measure)
    {
        var digitWidths = new float[10];
        float maxDigitW = 0f;
        float maxLineH = 0f;
        for (int d = 0; d < 10; d++)
        {
            Vector2 sz = measure(DigitStrings[d]);
            digitWidths[d] = sz.X;
            if (sz.X > maxDigitW) maxDigitW = sz.X;
            if (sz.Y > maxLineH) maxLineH = sz.Y;
        }
        Vector2 dotSz = measure(".");
        Vector2 tildeSz = measure("~");
        Vector2 labelSz = measure("PYRITE");
        if (dotSz.Y > maxLineH) maxLineH = dotSz.Y;
        if (tildeSz.Y > maxLineH) maxLineH = tildeSz.Y;
        if (labelSz.Y > maxLineH) maxLineH = labelSz.Y;
        return new TabularMetrics(maxDigitW, digitWidths, dotSz.X, tildeSz.X, MathF.Ceiling(maxLineH));
    }

    private static TabularMetrics GetTabularMetrics(ImFontPtr font, int fontPx)
    {
        var key = (font.FontId, font.CurrentRasterizerDensity, fontPx);
        if (s_tabularCache.TryGetValue(key, out var cached))
            return cached;

        ImGui.PushFont(font, fontPx);
        try
        {
            var created = MeasureTabularMetrics(static t => ImGui.CalcTextSize(t));
            if (s_tabularCache.Count >= 64) s_tabularCache.Clear();
            s_tabularCache[key] = created;
            return created;
        }
        finally
        {
            ImGui.PopFont();
        }
    }

    internal static float ComputeCountdownDotOffsetX(double remaining, bool estimated, TabularMetrics metrics)
    {
        if (!TrySplitCountdown(remaining, out string intPart, out _))
            return 0f;
        int intCols = Math.Max(2, intPart.Length);
        return (estimated ? metrics.TildeW : 0f) + intCols * metrics.DigitCellW;
    }

    private static void DrawTabularCountdown(
        ImDrawListPtr dl, ImFontPtr font, float fontPx, Vector2 origin,
        uint col, uint? shadowCol, double remaining, bool estimated, TabularMetrics metrics)
    {
        if (!TrySplitCountdown(remaining, out string intPart, out string fracPart))
            return;

        int intCols = Math.Max(2, intPart.Length);
        float x = origin.X;
        float y = origin.Y;

        if (estimated)
        {
            DrawCellGlyph(dl, font, fontPx, x, y, metrics.TildeW, metrics.TildeW, "~", col, shadowCol);
            x += metrics.TildeW;
        }

        x += (intCols - intPart.Length) * metrics.DigitCellW;
        for (int i = 0; i < intPart.Length; i++)
        {
            char ch = intPart[i];
            if (ch is >= '0' and <= '9')
            {
                int d = ch - '0';
                DrawCellGlyph(dl, font, fontPx, x, y, metrics.DigitCellW, metrics.DigitWidths[d], DigitStrings[d], col, shadowCol);
            }
            x += metrics.DigitCellW;
        }

        DrawCellGlyph(dl, font, fontPx, x, y, metrics.DotW, metrics.DotW, ".", col, shadowCol);
        x += metrics.DotW;

        for (int i = 0; i < fracPart.Length; i++)
        {
            char ch = fracPart[i];
            if (ch is >= '0' and <= '9')
            {
                int d = ch - '0';
                DrawCellGlyph(dl, font, fontPx, x, y, metrics.DigitCellW, metrics.DigitWidths[d], DigitStrings[d], col, shadowCol);
            }
            x += metrics.DigitCellW;
        }
    }

    private static void DrawCellGlyph(
        ImDrawListPtr dl, ImFontPtr font, float fontPx, float cellX, float y,
        float cellW, float glyphW, string glyph, uint col, uint? shadowCol)
    {
        float gx = cellX + (cellW - glyphW) * 0.5f;
        if (shadowCol.HasValue)
            dl.AddText(font, fontPx, new Vector2(gx + 1.5f, y + 1.5f), shadowCol.Value, glyph);
        dl.AddText(font, fontPx, new Vector2(gx, y), col, glyph);
    }

    internal static Vector2 MeasureCompactPillLayout(
        int fontPx, string text, TimerView view, bool demo, Settings s, bool townPaused,
        string? primerHint, bool showSp, bool editing, TabularMetrics metrics,
        Func<string, Vector2> measureText, Func<float, string, Vector2> measureHintText)
    {
        bool isTabular = IsTabularCountdown(view, townPaused);
        float textW;
        if (isTabular)
        {
            string prefix = CountdownPrefix(view, demo);
            float prefixW = metrics.GetPrefixWidth(prefix, measureText);
            textW = prefixW + metrics.MeasureCountdownWidth(view.Remaining!.Value, view.Estimated);
        }
        else
        {
            textW = measureText(text).X;
        }
        float textH = metrics.LineH;

        float padX = Math.Max(12f, fontPx * 0.42f);
        float padY = Math.Max(8f, fontPx * 0.28f);
        float glyphW = !townPaused && view.Reaction != Reaction.Any
            ? Math.Max(12f, fontPx * 0.56f) + Math.Max(6f, fontPx * 0.22f)
            : 0f;

        bool showBar = s.ShowDepletionBar && !townPaused &&
            view.Mode is TimerMode.Active or TimerMode.Paused && view.Remaining.HasValue && view.Duration > 0;
        float hintH = 0f;
        float hintW = 0f;
        if (primerHint is not null)
        {
            float hintPx = Math.Max(12f, fontPx * 0.44f);
            var hs = measureHintText(hintPx, primerHint);
            hintW = hs.X;
            hintH = hs.Y + 4f;
        }

        float w = Math.Max(editing ? 290f : 120f, Math.Max(glyphW + textW, hintW) + padX * 2f);
        float h = padY * 2f + textH + (showBar ? 11f : 0f) + (showSp ? 8f : 0f) + hintH + (editing ? 22f : 0f);
        return new Vector2(MathF.Ceiling(w), MathF.Ceiling(h));
    }

    private static Vector2 MeasureCompactPill(int fontPx, string text, TimerView view, bool demo, Settings s, bool townPaused, string? primerHint, bool showSp, bool editing)
    {
        var font = ImGui.GetFont();
        var metrics = GetTabularMetrics(font, fontPx);
        return MeasureCompactPillLayout(
            fontPx, text, view, demo, s, townPaused, primerHint, showSp, editing, metrics,
            t => CalcTextSizeAt(font, fontPx, t),
            (px, t) => CalcTextSizeAt(font, px, t));
    }

    private static void DrawCompactPill(
        ImDrawListPtr dl, Vector2 pMin, Vector2 size, int fontPx, string text, TimerView view, bool demo, Settings s,
        UiThemePalette palette, HudAlertFrame alert, Vector4 accent, float alpha, float pulse, bool townPaused,
        string? primerHint, bool showSp, float spFraction, bool editing)
    {
        Vector2 pMax = pMin + size;
        float rounding = 10f;
        Vector4 surfLowest = ToVec4(palette.SurfaceLowest, alpha);
        Vector4 surf = ToVec4(palette.Surface);
        Vector4 outline = ToVec4(palette.Outline);
        Vector4 primary = ToVec4(palette.Primary);

        Vector4 bgCol = alert.BlackBackground
            ? new Vector4(0.03f, 0.04f, 0.06f, Math.Min(1f, alpha + 0.08f))
            : surfLowest;
        Vector4 borderCol = alert.Enlarged
            ? Vector4.Lerp(CrimsonCol, FlashGoldCol, pulse)
            : pulse > 0f
                ? Vector4.Lerp(outline, accent, 0.45f + 0.55f * pulse)
                : new Vector4(accent.X, accent.Y, accent.Z, townPaused ? 0.45f : 0.78f);
        float borderThick = alert.Enlarged ? 2.5f : (pulse > 0f ? 2.0f : 1.5f);

        dl.AddRectFilled(pMin, pMax, U32(bgCol), rounding, ImDrawFlags.RoundCornersAll);
        dl.AddRect(pMin, pMax, U32(borderCol), rounding, borderThick, ImDrawFlags.RoundCornersAll);

        float padX = Math.Max(12f, fontPx * 0.42f);
        float padY = Math.Max(8f, fontPx * 0.28f);
        var font = ImGui.GetFont();
        var metrics = GetTabularMetrics(font, fontPx);
        float textH = metrics.LineH;

        float cursorX = pMin.X + padX;
        float cursorY = pMin.Y + padY;

        if (!townPaused && view.Reaction != Reaction.Any)
        {
            float glyphR = Math.Max(5f, fontPx * 0.26f);
            DrawVectorCvdGlyph(dl, view.Reaction, new Vector2(cursorX + glyphR, cursorY + textH * 0.5f), glyphR, U32(accent));
            cursorX += glyphR * 2f + Math.Max(6f, fontPx * 0.22f);
        }

        uint shadowU32 = U32(new Vector4(0, 0, 0, 0.85f));
        uint accentU32 = U32(accent);
        if (IsTabularCountdown(view, townPaused))
        {
            string prefix = CountdownPrefix(view, demo);
            dl.AddText(font, fontPx, new Vector2(cursorX + 1.5f, cursorY + 1.5f), shadowU32, prefix);
            dl.AddText(font, fontPx, new Vector2(cursorX, cursorY), accentU32, prefix);
            float prefixW = metrics.GetPrefixWidth(prefix, t => CalcTextSizeAt(font, fontPx, t));
            DrawTabularCountdown(dl, font, fontPx, new Vector2(cursorX + prefixW, cursorY), accentU32, shadowU32, view.Remaining!.Value, view.Estimated, metrics);
        }
        else
        {
            dl.AddText(font, fontPx, new Vector2(cursorX + 1.5f, cursorY + 1.5f), shadowU32, text);
            dl.AddText(font, fontPx, new Vector2(cursorX, cursorY), accentU32, text);
        }

        float rowY = cursorY + textH + 5f;
        float innerW = Math.Max(20f, size.X - padX * 2f);

        bool showBar = s.ShowDepletionBar && !townPaused &&
            view.Mode is TimerMode.Active or TimerMode.Paused && view.Remaining.HasValue && view.Duration > 0;
        if (showBar)
        {
            const float barH = 6f;
            Vector2 bMin = new(pMin.X + padX, rowY);
            Vector2 bMax = new(bMin.X + innerW, rowY + barH);
            dl.AddRectFilled(bMin, bMax, U32(surf), 3f, ImDrawFlags.RoundCornersAll);
            float frac = (float)HudPresentation.DepletionFraction(view);
            if (frac > 0.005f)
                dl.AddRectFilled(bMin, new Vector2(bMin.X + innerW * frac, bMax.Y), U32(accent), 3f, ImDrawFlags.RoundCornersAll);
            float warnFrac = (float)HudPresentation.WarningFraction(view, s.WarningSeconds);
            if (warnFrac > 0f && warnFrac < 1f)
            {
                float nx = bMin.X + innerW * warnFrac;
                dl.AddLine(new Vector2(nx, bMin.Y - 1f), new Vector2(nx, bMax.Y + 1f), U32(primary), 1.5f);
            }
            dl.AddRect(bMin, bMax, U32(outline), 3f, 1f, ImDrawFlags.RoundCornersAll);
            rowY += barH + 5f;
        }

        if (showSp)
        {
            DrawSpMicroBar(dl, new Vector2(pMin.X + padX, rowY), innerW, spFraction, surf);
            rowY += 8f;
        }

        if (primerHint is not null)
        {
            float hintPx = Math.Max(12f, fontPx * 0.44f);
            dl.AddText(font, hintPx, new Vector2(pMin.X + padX, rowY), U32(primary), primerHint);
        }

        if (editing) DrawEditFooter(dl, pMin, size, primary);
    }

    internal static Vector2 MeasureRadialDonut(int fontPx, string? primerHint, bool showSp, bool editing)
    {
        float diameter = Math.Clamp(fontPx * 3.1f, 84f, 240f);
        float w = Math.Max(editing ? 290f : 104f, diameter + 24f);
        float extraH = (showSp ? 10f : 0f) + (primerHint is not null ? Math.Max(16f, fontPx * 0.5f) : 0f) + (editing ? 24f : 0f);
        return new Vector2(MathF.Ceiling(w), MathF.Ceiling(diameter + 20f + extraH));
    }

    internal static Vector2 ComputeRadialCountdownOrigin(Vector2 center, double remaining, bool estimated, TabularMetrics numMetrics)
    {
        float totalW = numMetrics.MeasureCountdownWidth(remaining, estimated);
        return new Vector2(center.X - totalW * 0.5f, center.Y - numMetrics.LineH * 0.35f);
    }

    private static void DrawRadialDonut(
        ImDrawListPtr dl, Vector2 pMin, Vector2 size, int fontPx, TimerView view, Settings s,
        UiThemePalette palette, HudAlertFrame alert, Vector4 accent, float alpha, float pulse, bool townPaused,
        string? primerHint, bool showSp, float spFraction, bool editing)
    {
        Vector2 pMax = pMin + size;
        float diameter = Math.Clamp(fontPx * 3.1f, 84f, 240f);
        float radius = diameter * 0.5f - 6f;
        float thickness = Math.Clamp(radius * 0.17f, 5f, 14f);
        Vector2 center = new(pMin.X + size.X * 0.5f, pMin.Y + 10f + diameter * 0.5f);

        Vector4 surfLowest = ToVec4(palette.SurfaceLowest, alpha);
        Vector4 surf = ToVec4(palette.Surface);
        Vector4 outline = ToVec4(palette.Outline);
        Vector4 primary = ToVec4(palette.Primary);
        Vector4 muted = ToVec4(palette.OnSurfaceVariant);

        Vector4 bgCol = alert.BlackBackground
            ? new Vector4(0.03f, 0.04f, 0.06f, Math.Min(1f, alpha + 0.08f))
            : surfLowest;
        Vector4 ringBorder = alert.Enlarged
            ? Vector4.Lerp(CrimsonCol, FlashGoldCol, pulse)
            : pulse > 0f
                ? Vector4.Lerp(outline, accent, 0.5f + 0.5f * pulse)
                : outline;

        dl.AddRectFilled(pMin, pMax, U32(bgCol), 12f, ImDrawFlags.RoundCornersAll);
        dl.AddRect(pMin, pMax, U32(ringBorder), 12f, alert.Enlarged ? 2.5f : 1.25f, ImDrawFlags.RoundCornersAll);

        // Single-arc background track (never multi-ring per Blascheck et al. 2019!)
        dl.AddCircle(center, radius, U32(surf), 48, thickness);

        float frac = (float)HudPresentation.DepletionFraction(view);
        float startAngle = -MathF.PI * 0.5f;
        if (frac > 0.005f && !townPaused)
        {
            float endAngle = startAngle + MathF.PI * 2f * frac;
            dl.PathClear();
            dl.PathArcTo(center, radius, startAngle, endAngle, 48);
            dl.PathStroke(U32(accent), thickness, ImDrawFlags.None);
        }
        else if (view.Mode == TimerMode.Expired && !townPaused)
        {
            dl.AddCircle(center, radius, U32(Vector4.Lerp(CrimsonCol, FlashGoldCol, pulse)), 48, thickness);
        }

        float warnFrac = (float)HudPresentation.WarningFraction(view, s.WarningSeconds);
        if (warnFrac > 0f && warnFrac < 1f && !townPaused && view.Mode == TimerMode.Active)
        {
            float wa = startAngle + MathF.PI * 2f * warnFrac;
            Vector2 dir = new(MathF.Cos(wa), MathF.Sin(wa));
            dl.AddLine(center + dir * (radius - thickness * 0.65f), center + dir * (radius + thickness * 0.65f), U32(primary), 2f);
        }

        var font = ImGui.GetFont();
        int numPx = (int)MathF.Round(Math.Clamp(fontPx * 0.95f, 12f, 96f));
        var numMetrics = GetTabularMetrics(font, numPx);
        float numH = numMetrics.LineH;

        if (!townPaused && view.Reaction != Reaction.Any)
        {
            float glyphR = Math.Clamp(radius * 0.16f, 4f, 12f);
            DrawVectorCvdGlyph(dl, view.Reaction, new Vector2(center.X, center.Y - numH * 0.55f - glyphR * 0.6f), glyphR, U32(accent));
        }

        if (IsTabularCountdown(view, townPaused))
        {
            Vector2 numOrigin = ComputeRadialCountdownOrigin(center, view.Remaining!.Value, view.Estimated, numMetrics);
            DrawTabularCountdown(dl, font, numPx, numOrigin, U32(accent), null, view.Remaining.Value, view.Estimated, numMetrics);
        }
        else
        {
            string centerText = townPaused
                ? "TOWN"
                : view.Mode switch
                {
                    TimerMode.Active => "ACT",
                    TimerMode.Paused => "PAUSE",
                    TimerMode.Expired => "SPIKE!",
                    TimerMode.Error => "ERR",
                    _ => "READY"
                };
            Vector2 numSize = CalcTextSizeAt(font, numPx, centerText);
            dl.AddText(font, numPx, new Vector2(center.X - numSize.X * 0.5f, center.Y - numH * 0.35f), U32(accent), centerText);
        }

        string tag = townPaused ? "PAUSED" : HudPresentation.ShortTag(view.Reaction);
        float tagPx = Math.Max(11f, numPx * 0.42f);
        Vector2 tagSize = CalcTextSizeAt(font, tagPx, tag);
        dl.AddText(font, tagPx, new Vector2(center.X - tagSize.X * 0.5f, center.Y + numH * 0.48f), U32(muted), tag);

        float rowY = pMin.Y + 12f + diameter;
        if (showSp)
        {
            DrawSpMicroBar(dl, new Vector2(pMin.X + 12f, rowY), size.X - 24f, spFraction, surf);
            rowY += 9f;
        }
        if (primerHint is not null)
        {
            float hintPx = Math.Max(11f, fontPx * 0.42f);
            Vector2 hs = CalcTextSizeAt(font, hintPx, primerHint);
            dl.AddText(font, hintPx, new Vector2(pMin.X + (size.X - hs.X) * 0.5f, rowY), U32(primary), primerHint);
        }
        if (editing) DrawEditFooter(dl, pMin, size, primary);
    }

    private static void DrawSpMicroBar(ImDrawListPtr dl, Vector2 pos, float width, float fraction, Vector4 trackCol)
    {
        const float h = 4f;
        Vector2 pMax = new(pos.X + width, pos.Y + h);
        dl.AddRectFilled(pos, pMax, U32(trackCol), 2f, ImDrawFlags.RoundCornersAll);
        Vector4 spCol = fraction < 0.10f ? CrimsonCol : fraction < 0.25f ? PyriteCol : AstralBlue;
        if (fraction > 0.005f)
            dl.AddRectFilled(pos, new Vector2(pos.X + width * fraction, pMax.Y), U32(spCol), 2f, ImDrawFlags.RoundCornersAll);
    }

    private static void DrawEditFooter(ImDrawListPtr dl, Vector2 pMin, Vector2 size, Vector4 primary)
    {
        var font = ImGui.GetFont();
        dl.AddText(font, 11f, new Vector2(pMin.X + 8f, pMin.Y + size.Y - 17f), U32(primary), "DRAG • WHEEL FONT • CTRL+WHEEL ALPHA");
        Vector2 br = pMin + size;
        dl.AddTriangleFilled(new Vector2(br.X - 12f, br.Y - 2f), new Vector2(br.X - 2f, br.Y - 12f), new Vector2(br.X - 2f, br.Y - 2f), U32(primary));
    }

    private void HandleInteractiveHud(Vector2 hudMin, Vector2 hudSize, Vector2 vpPos, Vector2 vpSize, UiThemePalette palette, double now)
    {
        ImGui.SetNextWindowPos(hudMin, ImGuiCond.Always);
        ImGui.SetNextWindowSize(hudSize, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing;
        try
        {
            try
            {
                if (ImGui.Begin("##AlchHudEditOverlay", flags))
                {
                    ImGui.InvisibleButton("##AlchHudDragZone", hudSize);
                    bool hovered = ImGui.IsItemHovered();
                    bool active = ImGui.IsItemActive();
                    var io = ImGui.GetIO();
                    Vector2 mouse = ImGui.GetMousePos();
                    Vector2 br = hudMin + hudSize;
                    bool inResizeGrip = mouse.X >= br.X - 18f && mouse.Y >= br.Y - 18f;

                    if (active && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        _resizingHud = inResizeGrip;
                        _resizeStartMouseY = mouse.Y;
                        _resizeStartFont = _cachedSettings.FontSizePixels;
                    }

                    if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0.5f))
                    {
                        if (_resizingHud)
                        {
                            int deltaPx = (int)MathF.Round((mouse.Y - _resizeStartMouseY) * 0.45f);
                            int nextFont = Math.Clamp(_resizeStartFont + deltaPx, 10, 160);
                            if (nextFont != _cachedSettings.FontSizePixels)
                            {
                                _cachedSettings.FontSizePixels = nextFont;
                                _interactiveDirty = true;
                                _lastInteractiveSave = now;
                            }
                        }
                        else
                        {
                            Vector2 delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0.5f);
                            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Left);
                            float maxW = Math.Max(1f, vpSize.X - hudSize.X);
                            float maxH = Math.Max(1f, vpSize.Y - hudSize.Y);
                            _cachedSettings.HudX = Math.Clamp((hudMin.X + delta.X - vpPos.X) / maxW, 0, 1);
                            _cachedSettings.HudY = Math.Clamp((hudMin.Y + delta.Y - vpPos.Y) / maxH, 0, 1);
                            _interactiveDirty = true;
                            _lastInteractiveSave = now;
                        }
                    }
                    else if (!active)
                    {
                        _resizingHud = false;
                    }

                    if (hovered && Math.Abs(io.MouseWheel) > 0.01f)
                    {
                        int dir = io.MouseWheel > 0 ? 1 : -1;
                        if (io.KeyCtrl)
                            _cachedSettings.HudOpacity = Math.Clamp(Math.Round(_cachedSettings.HudOpacity + dir * 0.05, 2), 0.25, 1.0);
                        else
                            _cachedSettings.FontSizePixels = Math.Clamp(_cachedSettings.FontSizePixels + dir * 2, 10, 160);
                        _interactiveDirty = true;
                        _lastInteractiveSave = now;
                    }

                    var dl = ImGui.GetForegroundDrawList();
                    string lockText = $"{Math.Ceiling(Math.Max(0, _editUntil - now)):0}s";
                    dl.AddText(ImGui.GetFont(), 11f, new Vector2(hudMin.X + hudSize.X - 24f, hudMin.Y + 4f), U32(ToVec4(palette.Primary)), lockText);
                }
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            ImGui.PopStyleColor(1);
            ImGui.PopStyleVar(2);
        }
    }

    private void RenderQuickRespikePill(Vector2 hudMin, Vector2 hudSize, Vector2 vpPos, Vector2 vpSize, UiThemePalette palette)
    {
        const float pillW = 148f;
        const float pillH = 28f; // WCAG 2.5.8 >= 24px minimum target size
        float posX = Math.Clamp(hudMin.X + (hudSize.X - pillW) * 0.5f, vpPos.X, Math.Max(vpPos.X, vpPos.X + vpSize.X - pillW));
        float belowY = hudMin.Y + hudSize.Y + 6f;
        float posY = belowY + pillH <= vpPos.Y + vpSize.Y
            ? belowY
            : Math.Max(vpPos.Y, hudMin.Y - pillH - 6f);
        Vector2 pos = new(MathF.Round(posX), MathF.Round(posY));

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(pillW, pillH), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Button, ToVec4(palette.SurfaceHigh));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Lighten(ToVec4(palette.SurfaceHigh), 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ToVec4(palette.Primary));
        ImGui.PushStyleColor(ImGuiCol.Border, ToVec4(palette.Primary));
        ImGui.PushStyleColor(ImGuiCol.Text, ToVec4(palette.OnSurface));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing;
        try
        {
            try
            {
                if (ImGui.Begin("##AlchQuickRespikePill", flags))
                {
                    if (ImGui.Button("Quick Re-Spike", new Vector2(pillW, pillH)))
                        _engine.TryInvokeQuickRespike(out _statusMessage);
                }
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar(4);
        }
    }

    private void RenderControlWindow(Dh5Plugin plugin, Dh5HudSnapshot snap, UiThemePalette palette, double now)
    {
        PushTheme(palette);
        try
        {
            if (_resetWindowPending)
            {
                _resetWindowPending = false;
                var vp = ImGui.GetMainViewport();
                Vector2 centered = vp.Pos + new Vector2(
                    Math.Max(0f, (vp.Size.X - DefaultControlWindowSize.X) * 0.5f),
                    Math.Max(0f, (vp.Size.Y - DefaultControlWindowSize.Y) * 0.5f));
                ImGui.SetNextWindowPos(centered, ImGuiCond.Always);
                ImGui.SetNextWindowSize(DefaultControlWindowSize, ImGuiCond.Always);
            }
            else
            {
                ImGui.SetNextWindowSize(DefaultControlWindowSize, ImGuiCond.FirstUseEver);
            }

            bool visible = plugin.WindowVisible;
            try
            {
                if (ImGui.Begin(ControlWindowTitle, ref visible, ImGuiWindowFlags.NoCollapse))
                {
                    plugin.WindowVisible = visible;
                    RenderHeaderStrip(snap, palette);

                    if (ImGui.BeginTabBar("##AlchTabs", ImGuiTabBarFlags.FittingPolicyScroll))
                    {
                        try
                        {
                            if (BeginAccentTab("HUD", palette))
                            {
                                try { RenderHudTab(palette, now); }
                                finally { ImGui.EndTabItem(); }
                            }
                            if (BeginAccentTab("Spikes", palette))
                            {
                                try { RenderSpikesTab(snap, palette); }
                                finally { ImGui.EndTabItem(); }
                            }
                            if (BeginAccentTab("Quick-Cast", palette))
                            {
                                try { RenderQuickCastTab(palette); }
                                finally { ImGui.EndTabItem(); }
                            }
                            if (BeginAccentTab("Diagnostics", palette))
                            {
                                try { RenderDiagnosticsTab(snap, palette); }
                                finally { ImGui.EndTabItem(); }
                            }
                        }
                        finally
                        {
                            ImGui.EndTabBar();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(_statusMessage))
                    {
                        ImGui.Separator();
                        ImGui.TextColored(ToVec4(palette.Primary), _statusMessage);
                    }
                }
                else
                {
                    plugin.WindowVisible = visible;
                }
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            PopTheme();
        }
    }

    private static void RenderHeaderStrip(Dh5HudSnapshot snap, UiThemePalette palette)
    {
        var view = snap.View;
        Vector4 muted = ToVec4(palette.OnSurfaceVariant);
        Vector4 col = view.InTown && view.Mode == TimerMode.Paused
            ? muted
            : view.Mode == TimerMode.Expired ? CrimsonCol : ReactionColor(view.Reaction);
        ImGui.TextColored(col, $"{HudPresentation.CvdSymbol(view.Reaction)} {HudPresentation.Text(view, false)}");
        ImGui.SameLine();
        ImGui.TextColored(muted, $"  •  {view.Detail}");
        if (snap.SpellPointsMax is > 0 && snap.SpellPointsCurrent.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextColored(AstralBlue, $"  •  SP {snap.SpellPointsCurrent}/{snap.SpellPointsMax}");
        }
        ImGui.Spacing();
    }

    private void RenderHudTab(UiThemePalette palette, double now)
    {
        var s = _interactiveDirty ? _cachedSettings.Copy() : _engine.GetSettings();
        bool changed = false;

        bool enabled = s.HudEnabled;
        if (ImGui.Checkbox("Show In-Frame HUD", ref enabled)) { s.HudEnabled = enabled; changed = true; }

        int themeIndex = (int)s.Theme;
        if (ImGui.Combo("UI Theme", ref themeIndex, ThemeLabels, ThemeLabels.Length))
        {
            s.Theme = (UiTheme)themeIndex;
            changed = true;
        }

        int modeIndex = (int)s.HudMode;
        if (ImGui.Combo("HUD Style", ref modeIndex, HudModeLabels, HudModeLabels.Length))
        {
            s.HudMode = (HudDisplayMode)modeIndex;
            changed = true;
        }

        int fontPx = s.FontSizePixels;
        if (ImGui.SliderInt("Font Size (px)", ref fontPx, 10, 160)) { s.FontSizePixels = fontPx; changed = true; }

        float opacity = (float)s.HudOpacity;
        if (ImGui.SliderFloat("HUD Opacity", ref opacity, 0.25f, 1.0f, "%.2f")) { s.HudOpacity = Math.Round(opacity, 2); changed = true; }

        bool showBar = s.ShowDepletionBar;
        if (ImGui.Checkbox("Show Smooth Depletion Bar & Warning Notch", ref showBar)) { s.ShowDepletionBar = showBar; changed = true; }

        bool showSp = s.ShowSpellPointsBar;
        if (ImGui.Checkbox("Show Astral Blue Spell Points Micro-Bar", ref showSp)) { s.ShowSpellPointsBar = showSp; changed = true; }

        if (changed) CommitSettings(s);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Preview (12s + Alert)", new Vector2(165, 30))) StartPreview();
        ImGui.SameLine();
        bool editing = now < _editUntil;
        if (ImGui.Button(editing ? "Lock HUD Now" : "Move / Resize (20s)", new Vector2(165, 30)))
        {
            if (editing) _editUntil = now;
            else StartEditMode();
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset Layout", new Vector2(120, 30)))
        {
            _engine.ResetHudLayout();
            ResetLayout();
        }
        ImGui.TextColored(ToVec4(palette.OnSurfaceVariant), "While Move/Resize is active: drag HUD, scroll wheel for font px, Ctrl+wheel for opacity.");
    }

    private void RenderSpikesTab(Dh5HudSnapshot snap, UiThemePalette palette)
    {
        var s = _engine.GetSettings();
        bool changed = false;

        float warn = (float)s.WarningSeconds;
        if (ImGui.SliderFloat("Pre-Expiry Warning (sec)", ref warn, 0f, 10f, "%.1f s"))
        {
            s.WarningSeconds = Math.Round(warn, 1);
            changed = true;
        }

        int alertSec = s.RespikeAlertSeconds;
        if (ImGui.SliderInt("Re-Spike Alert Pulse (sec, 0 = off)", ref alertSec, 0, 60))
        {
            s.RespikeAlertSeconds = alertSec;
            changed = true;
        }

        bool sound = s.SoundOnExpiry;
        if (ImGui.Checkbox("Play Sound on Expiry (MessageBeep)", ref sound)) { s.SoundOnExpiry = sound; changed = true; }

        bool town = s.SuppressAlertsInTown;
        if (ImGui.Checkbox("Suppress Re-Spike Alerts in Town / Shrines", ref town)) { s.SuppressAlertsInTown = town; changed = true; }

        if (changed) CommitSettings(s);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(ToVec4(palette.Primary), "Alchemist Reaction Primer Reference (CVD-Safe)");
        ImGui.TextColored(PyriteCol, "  ▲ PYR  Pyrite    (Crimsonite + Gildleaf)  → Cast Ceruleite to break");
        ImGui.TextColored(OrchidiumCol, "  ◆ ORC  Orchidium (Crimsonite + Ceruleite) → Cast Gildleaf to break");
        ImGui.TextColored(VerdaniteCol, "  ✚ VRD  Verdanite (Gildleaf + Ceruleite)   → Cast Crimsonite to break");
        if (snap.View.BaseStance != Reaction.Any)
        {
            string? next = ReactionMatcher.NextPrimerFor(snap.View.BaseStance);
            ImGui.Spacing();
            ImGui.TextColored(ToVec4(palette.Primary), $"Detected Stance: {snap.View.BaseStance}  →  Next Primer: {next}");
        }
    }

    private void RenderQuickCastTab(UiThemePalette palette)
    {
        var s = _engine.GetSettings();
        bool changed = false;

        ImGui.TextWrapped("Configure a cheap primer-flip SLA or spell hotbar slot for one-click or /alch respike invocation without dropping mouselook or target selection (1 player action = 1 InvokeHotbar). Leave Bar/Slot at 0 to keep disabled.");
        ImGui.Spacing();

        bool enabled = s.QuickRespikeEnabled;
        if (ImGui.Checkbox("Enable Quick Re-Spike (InvokeHotbar)", ref enabled)) { s.QuickRespikeEnabled = enabled; changed = true; }

        int bar = s.QuickRespikeBar;
        if (ImGui.SliderInt("Hotbar Number (0 = unset, 1–20)", ref bar, 0, 20)) { s.QuickRespikeBar = bar; changed = true; }

        int slot = s.QuickRespikeSlot;
        if (ImGui.SliderInt("Slot Number (0 = unset, 1–10)", ref slot, 0, 10)) { s.QuickRespikeSlot = slot; changed = true; }

        if (changed) CommitSettings(s);

        ImGui.Spacing();
        if (ImGui.Button("Test Quick Re-Spike Now", new Vector2(200, 30)))
            _engine.TryInvokeQuickRespike(out _statusMessage);

        ImGui.Spacing();
        ImGui.Separator();
        Vector4 muted = ToVec4(palette.OnSurfaceVariant);
        ImGui.TextColored(ToVec4(palette.Primary), "Chat Commands (/alch)");
        ImGui.TextColored(muted, "  /alch status                     — Show current spike, stance, and SP");
        ImGui.TextColored(muted, "  /alch warn <0-10>                — Set pre-expiry warning seconds");
        ImGui.TextColored(muted, "  /alch hud [on|off|pill|donut]    — Toggle HUD or switch gauge mode");
        ImGui.TextColored(muted, "  /alch town [on|off]              — Toggle town/shrine alert suppression");
        ImGui.TextColored(muted, "  /alch theme <eberron|cannith|feywild> — Switch DDO Material Dark theme");
        ImGui.TextColored(muted, "  /alch respike                    — Trigger configured Quick Re-Spike slot");
        ImGui.TextColored(muted, "  /alch reset                      — Reset HUD position, size, and opacity");
    }

    private void RenderDiagnosticsTab(Dh5HudSnapshot snap, UiThemePalette palette)
    {
        var s = _engine.GetSettings();
        ImGui.TextColored(ToVec4(palette.OnSurfaceVariant),
            $"Character: {(snap.CharacterId.HasValue ? snap.CharacterId.Value.ToString("X", CultureInfo.InvariantCulture) : "Offline")} | Town: {(snap.InTown ? "Yes" : "No")} | /alch: {(_engine.ChatCommandRegistered ? "Active" : "Unavailable")}");
        ImGui.Spacing();

        if (ImGui.BeginTable("##EffectsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 195)))
        {
            try
            {
                ImGui.TableSetupColumn("DID", ImGuiTableColumnFlags.WidthFixed, 75);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Dur", ImGuiTableColumnFlags.WidthFixed, 48);
                ImGui.TableSetupColumn("Seen", ImGuiTableColumnFlags.WidthFixed, 58);
                ImGui.TableSetupColumn("Override", ImGuiTableColumnFlags.WidthFixed, 82);
                ImGui.TableHeadersRow();

                var effects = _engine.GetEffects().ToDictionary(x => x.Did);
                foreach (int id in s.EffectMappings.Keys)
                    if (!effects.ContainsKey(id)) effects[id] = new EffectInfo(id, "(saved mapping)", null, 0);

                foreach (var effect in effects.Values.OrderByDescending(e => ReactionMatcher.Match(e.Name, e.Duration).HasValue).ThenBy(e => e.Name))
                {
                    ImGui.PushID(effect.Did);
                    try
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        bool isSel = _selectedEffectDid == effect.Did;
                        if (ImGui.Selectable(effect.Did.ToString(CultureInfo.InvariantCulture), isSel, ImGuiSelectableFlags.SpanAllColumns))
                            _selectedEffectDid = effect.Did;
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(effect.Name) ? "(name unavailable)" : effect.Name);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(effect.Duration?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?");
                        ImGui.TableNextColumn();
                        double age = Dh5TimerEngine.Now - effect.LastSeen;
                        ImGui.TextUnformatted(effect.LastSeen == 0 ? "Saved" : age < 2 ? "Now" : $"{age:0}s");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.EffectMappings.TryGetValue(effect.Did, out var m) ? m.ToString() : "Auto");
                    }
                    finally
                    {
                        ImGui.PopID();
                    }
                }
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(130);
        ImGui.Combo("##OverrideReaction", ref _selectedReactionIndex, ReactionNames, ReactionNames.Length);
        ImGui.SameLine();
        if (ImGui.Button("Track Selected", new Vector2(118, 28)))
        {
            if (_selectedEffectDid == 0) _statusMessage = "Select an effect row first.";
            else
            {
                s.EffectMappings[_selectedEffectDid] = (Reaction)_selectedReactionIndex;
                CommitSettings(s);
                _statusMessage = $"Mapped DID {_selectedEffectDid} -> {(Reaction)_selectedReactionIndex}.";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear Override", new Vector2(118, 28)))
        {
            if (_selectedEffectDid != 0 && s.EffectMappings.Remove(_selectedEffectDid))
            {
                CommitSettings(s);
                _statusMessage = $"Cleared override for DID {_selectedEffectDid}.";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Export JSON", new Vector2(105, 28)))
        {
            try { _statusMessage = "Exported: " + _engine.ExportDiagnostics(); }
            catch (Exception e) { _statusMessage = "Export failed: " + e.Message; }
        }
    }

    private void CommitSettings(Settings s)
    {
        try
        {
            _interactiveDirty = false;
            _engine.SaveSettings(s);
            _cachedSettings = _engine.GetSettings();
            _cachedRevision = _engine.SettingsRevision;
        }
        catch (Exception e)
        {
            _statusMessage = "Save failed: " + e.Message;
        }
    }

    private static bool BeginAccentTab(string label, UiThemePalette palette)
    {
        bool open = ImGui.BeginTabItem(label);
        if (open)
        {
            Vector2 rMin = ImGui.GetItemRectMin();
            Vector2 rMax = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(
                new Vector2(rMin.X + 2f, rMax.Y - 2.5f),
                new Vector2(rMax.X - 2f, rMax.Y),
                U32(ToVec4(palette.Primary)));
        }
        return open;
    }

    private static void DrawVectorCvdGlyph(ImDrawListPtr dl, Reaction reaction, Vector2 c, float r, uint col)
    {
        switch (reaction)
        {
            case Reaction.Pyrite:
                dl.AddTriangleFilled(new Vector2(c.X, c.Y - r), new Vector2(c.X + r, c.Y + r), new Vector2(c.X - r, c.Y + r), col);
                break;
            case Reaction.Orchidium:
                dl.AddTriangleFilled(new Vector2(c.X, c.Y - r), new Vector2(c.X + r, c.Y), new Vector2(c.X - r, c.Y), col);
                dl.AddTriangleFilled(new Vector2(c.X - r, c.Y), new Vector2(c.X + r, c.Y), new Vector2(c.X, c.Y + r), col);
                break;
            case Reaction.Verdanite:
                float arm = Math.Max(2f, r * 0.36f);
                dl.AddRectFilled(new Vector2(c.X - arm, c.Y - r), new Vector2(c.X + arm, c.Y + r), col);
                dl.AddRectFilled(new Vector2(c.X - r, c.Y - arm), new Vector2(c.X + r, c.Y + arm), col);
                break;
            default:
                dl.AddCircleFilled(c, r, col, 16);
                break;
        }
    }

    private static Vector4 ReactionColor(Reaction r) => r switch
    {
        Reaction.Pyrite => PyriteCol,
        Reaction.Orchidium => OrchidiumCol,
        Reaction.Verdanite => VerdaniteCol,
        _ => GenericCol
    };

    private static void PushTheme(UiThemePalette palette)
    {
        Vector4 sLowest = ToVec4(palette.SurfaceLowest);
        Vector4 sLow = ToVec4(palette.SurfaceLow);
        Vector4 surf = ToVec4(palette.Surface);
        Vector4 sHigh = ToVec4(palette.SurfaceHigh);
        Vector4 outline = ToVec4(palette.Outline);
        Vector4 primary = ToVec4(palette.Primary);
        Vector4 primaryActive = Lighten(primary, 0.09f);
        Vector4 onSurface = ToVec4(palette.OnSurface);
        Vector4 onVariant = ToVec4(palette.OnSurfaceVariant);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));

        ImGui.PushStyleColor(ImGuiCol.WindowBg, sLowest);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, sLow);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, sHigh);
        ImGui.PushStyleColor(ImGuiCol.Border, outline);
        ImGui.PushStyleColor(ImGuiCol.Text, onSurface);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, onVariant);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, sLow);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, surf);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, surf);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, sHigh);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, sHigh);
        ImGui.PushStyleColor(ImGuiCol.Button, surf);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, sHigh);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, primary);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, primary);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, primary);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, primaryActive);
        ImGui.PushStyleColor(ImGuiCol.Tab, sLow);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, sHigh);
        ImGui.PushStyleColor(ImGuiCol.TabSelected, surf);
        ImGui.PushStyleColor(ImGuiCol.Header, sHigh);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Lighten(sHigh, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, primary);
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, surf);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, sLow);
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, sLowest);
        ImGui.PushStyleColor(ImGuiCol.Separator, outline);
    }

    private static void PopTheme()
    {
        ImGui.PopStyleColor(27);
        ImGui.PopStyleVar(8);
    }

    private static Vector4 ToVec4(ThemeRgb c, float a = 1f) => new(c.R / 255f, c.G / 255f, c.B / 255f, a);
    private static Vector4 Hex(byte r, byte g, byte b, float a) => new(r / 255f, g / 255f, b / 255f, a);
    private static Vector4 Lighten(Vector4 c, float d) => new(Math.Min(1f, c.X + d), Math.Min(1f, c.Y + d), Math.Min(1f, c.Z + d), c.W);
    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);
}
