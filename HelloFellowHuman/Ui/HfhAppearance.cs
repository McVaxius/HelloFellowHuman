using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using AethertekUI.Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;

namespace HelloFellowHuman.Ui;

internal sealed class HfhAppearance : IDisposable
{
    private readonly System.Collections.Generic.Dictionary<Dalamud.Interface.Windowing.IWindow, AethertekUI.MaterialWindowOpacity> windowOpacities = new();
    private readonly AethertekUI.MaterialWindowOpacity fontStatusOpacity = new();
    private readonly Plugin plugin;
    private readonly MaterialTextHost shapedText;
    private UiText text;
    private HfhFonts fonts;
    private MaterialTheme theme;
    private readonly Dictionary<Dalamud.Interface.Windowing.Window, AethertekUI.Dalamud.MaterialAppearanceTitleBar> appearanceTitleBars = new();
    private readonly MaterialWindowFold fontStatusMotion = new();
    private readonly MaterialWindowDecorations fontStatusDecorations = new();
    private readonly MaterialOptions<string> languages = new(UiText.Languages.Select(l => new MaterialOption<string>(l.Code, l.Code, l.Name)).ToArray());
    private string appliedLanguage = "";
    private uint appliedAccent;
    private Vector3 accentDraft;
    private MaterialColorSettings colorDraft = new();
    private MaterialColorSettings? appliedSavedColors;
    private MaterialColorSettings? appliedColors;
    private int checkedGeneration = -1;
    private int checkedHindiGeneration = -1;
    private bool fontIssueLogged;
    private MaterialUiSoundPlayer? uiSoundPlayer;
    private bool appliedSoundsMuted = true;
    private readonly Dictionary<Window, TitleBarButton> soundTitleButtons = new();

    private void Apply()
    {
        var language = UiText.Languages.Any(l => l.Code == plugin.Configuration.UiLanguage) ? plugin.Configuration.UiLanguage : "en";
        if (language != appliedLanguage)
        {
            fonts?.Dispose();
            text?.Dispose();
            text = new(language, PushFont);
            fonts = new(Plugin.PluginInterface.UiBuilder.FontAtlas, text.GlyphRanges(), language);
            appliedLanguage = language;
            checkedGeneration = -1;
            checkedHindiGeneration = -1;
            fontIssueLogged = false;
        }
        var savedColors = plugin.Configuration.UiColors ?? new MaterialColorSettings();
        if (savedColors != appliedSavedColors)
        {
            colorDraft = savedColors;
            appliedSavedColors = savedColors;
        }
        if (theme is null || appliedAccent != (plugin.Configuration.UiAccentRgb & 0xFFFFFF) || colorDraft != appliedColors)
        {
            appliedAccent = plugin.Configuration.UiAccentRgb & 0xFFFFFF;
            appliedColors = colorDraft;
            theme = HfhPresentation.Theme(appliedAccent, colorDraft);
            theme.Feedback = PlayLcarsSound;
            if (!theme.IsLcars) uiSoundPlayer?.Stop();
            var rgb = HfhPresentation.Rgb(appliedAccent);
            accentDraft = new(rgb.X, rgb.Y, rgb.Z);
        }
        theme.Density = plugin.Configuration.UiCompact ? MaterialDensity.Compact : MaterialDensity.Standard;
        if (appliedSoundsMuted != plugin.Configuration.UiLcarsSoundsMuted)
        {
            appliedSoundsMuted = plugin.Configuration.UiLcarsSoundsMuted;
            if (appliedSoundsMuted) uiSoundPlayer?.Stop();
        }
    }

    internal void Draw(WindowSystem windows)
    {
        Apply();
        if (!windows.Windows.Any(window => window.IsOpen)) return;
        using var resources = text.Enter();
        using var shaping = shapedText.Push();
        if (fonts.Ready && checkedHindiGeneration != fonts.Generation)
        {
            var generation = fonts.Generation;
            var hindiAvailable = true;
            foreach (var size in HfhPresentation.FontSizes)
                hindiAvailable &= shapedText.Renderer.TryCheckGlyphs(["हिन्दी"], size * ImGuiHelpers.GlobalScale, out _);
            languages.Replace(UiText.Languages.Select(l => new MaterialOption<string>(l.Code, l.Code,
                l.Code == "hi" && !hindiAvailable ? "Hindi (unavailable)" : l.Name, l.Code == "hi" && !hindiAvailable)).ToArray());
            checkedHindiGeneration = generation;
        }
        if (fonts.Ready && checkedGeneration != fonts.Generation)
        {
            try
            {
                var generation = fonts.Generation;
                foreach (var size in HfhPresentation.FontSizes)
                    shapedText.Renderer.CheckGlyphs(text.RequiredText, size * ImGuiHelpers.GlobalScale);
                fonts.CheckGlyphs(text.RequiredText);
                checkedGeneration = generation;
            }
            catch (Exception ex)
            {
                if (!fontIssueLogged) { Plugin.Log.Error(ex, "[HFH] Required UI glyph coverage failed."); fontIssueLogged = true; }
            }
        }
        using var palette = MaterialTheme.Push(theme, ImGuiHelpers.GlobalScale, MaterialStyleMode.ColorsOnly);
        using var chrome = MaterialWindowChrome.Push();
        if (!fonts.Ready || checkedGeneration != fonts.Generation)
        {
            if (!fontIssueLogged && fonts.LoadException is { } error) { Plugin.Log.Error(error, "[HFH] Required UI fonts failed to load."); fontIssueLogged = true; }
            ImGui.SetNextWindowSize(new Vector2(460 * ImGuiHelpers.GlobalScale, 0));
            fontStatusMotion.PreDraw("Hello Fellow Human##FontStatus", null, null, reducedMotion: false, prepareDecorations: fontStatusDecorations.Prepare);
            try
            {
                bool visible;
                using (MaterialSkin.BackdropStyle())
                    visible = ImGui.Begin("Hello Fellow Human##FontStatus", ImGuiWindowFlags.AlwaysAutoResize);
                if (visible)
                {
                    MaterialSkin.Backdrop();
                    fontStatusDecorations.Paint();
                    var loading = fonts.LoadException is null && !fontIssueLogged;
                    if (appliedLanguage == "hi")
                    {
                        ImGui.TextWrapped(loading ? "Loading Hindi UI fonts..." : "Hindi UI fonts are unavailable. See the plugin log.");
                        if (!loading && ImGui.Button("Use English")) { plugin.Configuration.UiLanguage = "en"; plugin.SaveConfig(); }
                    }
                    else MaterialText.TextWrapped(UiText.T(loading ? "Loading UI fonts..." : "UI fonts failed to load. See the plugin log."));
                }
            }
            finally
            {
                ImGui.End();
                fontStatusDecorations.Paint();
                fontStatusMotion.PostDraw();
                ApplyWindowOpacity(fontStatusOpacity, "Hello Fellow Human##FontStatus");
            }
            return;
        }
        using var style = new MaterialStyleScope();
        var s = ImGuiHelpers.GlobalScale;
        style.Style(ImGuiStyleVar.WindowPadding, new Vector2(plugin.Configuration.UiCompact ? 14 : 17) * s);
        style.Style(ImGuiStyleVar.ItemSpacing, new Vector2(plugin.Configuration.UiCompact ? 8 : 12, plugin.Configuration.UiCompact ? 5 : 10) * s);
        style.Style(ImGuiStyleVar.FramePadding, new Vector2(plugin.Configuration.UiCompact ? 10 : 14, plugin.Configuration.UiCompact ? 4 : 7) * s);
        style.Style(ImGuiStyleVar.CellPadding, new Vector2(plugin.Configuration.UiCompact ? 6 : 10, plugin.Configuration.UiCompact ? 4 : 8) * s);
        style.Style(ImGuiStyleVar.FrameRounding, 4 * s);
        style.Style(ImGuiStyleVar.ChildRounding, 4 * s);
        using var body = fonts.Push(UiFontRole.Body);
        using var nativeButtons = MaterialLcars.NativeButtonStyle();
        if (theme.IsLcars)
        {
            style.Color(ImGuiCol.CheckMark, MaterialLcars.ControlBlue);
            style.Color(ImGuiCol.SliderGrab, MaterialLcars.ControlBlue);
            style.Color(ImGuiCol.SliderGrabActive, MaterialLcars.ControlBlue);
        }
        PrepareAppearanceTitleBars(windows);
        using (MaterialSkin.BackdropStyle())
            windows.Draw();
        foreach (var titlebar in appearanceTitleBars.Values) titlebar.DrawPopups();
        foreach (var window in windows.Windows)
        {
            if (!windowOpacities.TryGetValue(window, out var opacity))
                windowOpacities.Add(window, opacity = new());
            ApplyWindowOpacity(opacity, window.WindowName);
        }
    }

    private bool DrawColourEditor(string id)
    {
        var changed = MaterialAppearanceSelector.DrawColors(id, ref colorDraft, ref accentDraft,
            new(UiText.T("Color"), UiText.T("Language"), UiText.T("Teal"), UiText.T("Blue"), UiText.T("Pink"), UiText.T("Custom RGB")),
            new(UiText.T("Auto"), UiText.T("Picker"), UiText.T("Default"), UiText.T("Background, controls and text follow your theme."),
                UiText.T("Theme colour"), UiText.T("Choose each colour independently."), UiText.T("Background"), UiText.T("Controls"),
                UiText.T("Text"), UiText.T("Aa  Preview text"), UiText.T("Control"),
                UiText.T("Switch to Picker to set each colour yourself."), UiText.T("Switch back to Auto to use the automatic theme."))
                {
                    Lcars = UiText.T("LCARS"), LcarsHelp = UiText.T("LCARS panels, bars and controls"),
                    LcarsBar1 = UiText.T("LCARS bar 1"), LcarsBar2 = UiText.T("LCARS bar 2"), LcarsBar3 = UiText.T("LCARS bar 3"),
                    Glassmorphism = UiText.T("Glassmorphism"), Spatial = UiText.T("Spatial"), Cyberpunk = UiText.T("Cyberpunk"),
                    Reset = UiText.T("Reset"), ResetColors = UiText.T("Reset colours"),
                    Animation = UiText.T("Animation"), AnimationSpeed = UiText.T("Animation speed (0.1–10x; default 1x)"),
                    WaveColors = UiText.T("Wave colours"), GlassWave1 = UiText.T("Wave colour 1"),
                    GlassWave2 = UiText.T("Wave colour 2"), GlassWave3 = UiText.T("Wave colour 3"), ResetWaveColors = UiText.T("Reset wave colours"),
                },
            new MaterialColorSettings(), MaterialColorSettings.Rgb(HfhPresentation.ReferenceAccent),
            plugin.Configuration.UiCompact ? 298 : 316);
        if (!changed) return false;
        plugin.Configuration.UiColors = colorDraft;
        plugin.Configuration.UiAccentRgb = MaterialColorSettings.ToRgb(accentDraft);
        return true;
    }

    internal void DrawSelector()
    {
        var language = appliedLanguage;
        using var controls = MaterialControls.Push(HfhPresentation.Controls());
        var colorChanged = DrawColourEditor("appearance");
        var languageChanged = MaterialAppearanceSelector.DrawLanguage("appearance", ref language, languages, 140);
        if (languageChanged) plugin.Configuration.UiLanguage = language;
        if (colorChanged || languageChanged) plugin.SaveConfig();
    }

    internal float LanguageWidth()
    {
        var metrics = HfhPresentation.Controls();
        return Math.Max(140 * MaterialTheme.Metrics.Scale, MathF.Ceiling(MaterialText.Measure(languages.LabelFor(appliedLanguage, "Select...")).X
            + metrics.Height + 3 * metrics.Gap + Math.Min(metrics.IconSize, metrics.Height)));
    }

    internal void DrawAccent()
    {
        using var controls = MaterialControls.Push(HfhPresentation.Controls());
        if (!MaterialAppearanceSelector.DrawAccent("appearance", ref accentDraft,
            new(UiText.T("Color"), UiText.T("Language"), UiText.T("Teal"), UiText.T("Blue"), UiText.T("Pink"), UiText.T("Custom RGB")), HfhPresentation.ControlHeight)) return;
        plugin.Configuration.UiAccentRgb = ((uint)Math.Clamp((int)MathF.Round(accentDraft.X * 255), 0, 255) << 16)
            | ((uint)Math.Clamp((int)MathF.Round(accentDraft.Y * 255), 0, 255) << 8) | (uint)Math.Clamp((int)MathF.Round(accentDraft.Z * 255), 0, 255);
        plugin.SaveConfig();
    }

    internal void DrawLanguage()
    {
        var language = appliedLanguage;
        using var controls = MaterialControls.Push(HfhPresentation.Controls());
        if (!MaterialAppearanceSelector.DrawLanguage("appearance", ref language, languages, 140)) return;
        plugin.Configuration.UiLanguage = language;
        plugin.SaveConfig();
    }

    internal UiText.Scope EnterText() => text.Enter();

    internal HfhAppearance(Plugin plugin)
    {
        this.plugin = plugin;
        shapedText = new(Plugin.TextureProvider);
        appliedLanguage = UiText.Languages.Any(l => l.Code == plugin.Configuration.UiLanguage) ? plugin.Configuration.UiLanguage : "en";
        text = new(appliedLanguage, PushFont);
        fonts = new(Plugin.PluginInterface.UiBuilder.FontAtlas, text.GlyphRanges(), appliedLanguage);
        appliedAccent = plugin.Configuration.UiAccentRgb & 0xFFFFFF;
        colorDraft = plugin.Configuration.UiColors ?? new MaterialColorSettings();
        appliedSavedColors = appliedColors = colorDraft;
        theme = HfhPresentation.Theme(appliedAccent, colorDraft);
        theme.Feedback = PlayLcarsSound;
        if (!theme.IsLcars) uiSoundPlayer?.Stop();
        var rgb = HfhPresentation.Rgb(appliedAccent);
        accentDraft = new(rgb.X, rgb.Y, rgb.Z);
    }
    private IDisposable PushFont(UiFontRole role) => fonts.Push(role);

    private bool LcarsActive => plugin.Configuration.UiColors?.Mode == MaterialColorMode.Lcars;

    private void PlayLcarsSound(MaterialUiSoundCue cue)
    {
        if (!LcarsActive || plugin.Configuration.UiLcarsSoundsMuted) return;
        (uiSoundPlayer ??= new MaterialUiSoundPlayer()).TryPlay(cue);
    }

    internal void SetLcarsSounds(bool enabled)
    {
        plugin.Configuration.UiLcarsSoundsMuted = !enabled;
        if (!enabled) uiSoundPlayer?.Stop();
        plugin.SaveConfig();
    }

    private void DrawLcarsSounds()
    {
        MaterialSettings.Section(UiText.T("LCARS UI sounds"));
        ImGui.BeginDisabled(!LcarsActive);
        try
        {
            var enabled = !plugin.Configuration.UiLcarsSoundsMuted;
            if (UiGui.Checkbox("UI sounds", ref enabled)) SetLcarsSounds(enabled);
            UiGui.TextWrapped("Custom UI tones. The titlebar speaker toggles the same setting.");
            ImGui.BeginDisabled(plugin.Configuration.UiLcarsSoundsMuted);
            try
            {
                var activeTheme = MaterialTheme.Current;
                var feedback = activeTheme.Feedback;
                bool preview;
                try { activeTheme.Feedback = null; preview = UiGui.Button("Preview sound"); }
                finally { activeTheme.Feedback = feedback; }
                MaterialLcars.Feedback(preview, MaterialUiSoundCue.Preview);
            }
            finally { ImGui.EndDisabled(); }
            if (uiSoundPlayer?.Error is not null) UiGui.TextWrapped("Audio output is unavailable.");
        }
        finally { ImGui.EndDisabled(); }
        if (!LcarsActive) UiGui.TextDisabled("Select LCARS to use UI sounds.");
        else if (plugin.Configuration.UiLcarsSoundsMuted) UiGui.TextDisabled("Turn on UI sounds to preview.");
    }

    public void Dispose() { uiSoundPlayer?.Dispose(); shapedText.Dispose(); fonts?.Dispose(); text?.Dispose(); }

    internal void ApplyWindowOpacity(AethertekUI.MaterialWindowOpacity opacity, string windowName)
    {
        var config = plugin.Configuration;
        opacity.Apply(windowName, config.UiWindowOpacityPercent / 100f, config.UiTransparencyEnabled,
            config.UiAutoFade, config.UiFadedOpacityPercent / 100f, config.UiUnfocusedDelaySeconds);
    }

    internal void DrawTransparencyToggle()
    {
        var enabled = plugin.Configuration.UiTransparencyEnabled;
        if (UiGui.Checkbox("Transparency###window-transparency-main", ref enabled))
        { plugin.Configuration.UiTransparencyEnabled = enabled; plugin.SaveConfig(); }
    }

    internal void DrawWindowSettings()
    {
        var showTitlebarStatus = plugin.Configuration.UiShowTitlebarStatus;
        if (UiGui.Checkbox(UiText.T("Show titlebar status") + "###UiShowTitlebarStatus", ref showTitlebarStatus))
        { plugin.Configuration.UiShowTitlebarStatus = showTitlebarStatus; plugin.SaveConfig(); }
        var detailedTitlebarStatus = plugin.Configuration.UiDetailedTitlebarStatus;
        if (UiGui.Checkbox(UiText.T("Detailed titlebar status") + "###UiDetailedTitlebarStatus", ref detailedTitlebarStatus))
        { plugin.Configuration.UiDetailedTitlebarStatus = detailedTitlebarStatus; plugin.SaveConfig(); }
        var config = plugin.Configuration;
        var changed = false;
        var compactVisible = config.UiCompactVisibleOnMainWindow;
        if (UiGui.Checkbox("Compact visible on main window###window-compact-visible", ref compactVisible))
        { config.UiCompactVisibleOnMainWindow = compactVisible; changed = true; }
        var transparencyVisible = config.UiTransparencyVisibleOnMainWindow;
        if (UiGui.Checkbox("Transparency visible on main window###UiTransparencyVisibleOnMainWindowSettings", ref transparencyVisible))
        { config.UiTransparencyVisibleOnMainWindow = transparencyVisible; changed = true; }
        var languageVisible = config.UiLanguageVisibleOnMainWindow;
        if (UiGui.Checkbox("Language visible on main window###window-language-visible", ref languageVisible))
        { config.UiLanguageVisibleOnMainWindow = languageVisible; changed = true; }
        if (changed) plugin.SaveConfig();
        DrawTransparencySettings();
        DrawLcarsSounds();
    }

    private void PrepareAppearanceTitleBars(Dalamud.Interface.Windowing.WindowSystem windows)
    {
        foreach (var window in windows.Windows.OfType<Dalamud.Interface.Windowing.Window>())
        {
            if ((window.Flags & ImGuiWindowFlags.NoTitleBar) != 0) continue;
            if (!appearanceTitleBars.ContainsKey(window))
                appearanceTitleBars.Add(window, AethertekUI.Dalamud.MaterialAppearanceTitleBar.Register(window,
                    () => new(UiText.T("Language"), UiText.T("Compact mode"), UiText.T("Transparency"), UiText.T("Color")),
                    DrawLanguage,
                    () => { plugin.Configuration.UiCompact = !plugin.Configuration.UiCompact; plugin.SaveConfig(); },
                    DrawTransparencySettings, DrawTitleBarColour,
                    () =>
                    {
                        plugin.Configuration.KrangleEnabled = !plugin.Configuration.KrangleEnabled;
                        plugin.SaveConfig();
                        global::HelloFellowHuman.Services.KrangleService.ClearCache();
                    },
                    () => UiText.T("Krangle") + ": " + UiText.T(plugin.Configuration.KrangleEnabled ? "Yes" : "No"),
                    () => window is global::HelloFellowHuman.Windows.ConfigWindow
                        ? new(plugin.Configuration.UiLanguageVisibleOnMainWindow, plugin.Configuration.UiCompactVisibleOnMainWindow,
                            plugin.Configuration.UiTransparencyVisibleOnMainWindow, true)
                        : new(true, true, true, true)));
            appearanceTitleBars[window].RefreshVisibility();
            appearanceTitleBars[window].SetLcarsIcons(theme.IsLcars);
            if (!soundTitleButtons.TryGetValue(window, out var sound))
            {
                sound = new TitleBarButton
                {
                    Priority = (int)Math.Max(int.MinValue + 1L, window.TitleBarButtons.Select(static button => (long)button.Priority).DefaultIfEmpty(0).Min() - 1),
                    IconOffset = new(2, 1),
                    Click = mouse => { if (mouse == ImGuiMouseButton.Left && LcarsActive) SetLcarsSounds(plugin.Configuration.UiLcarsSoundsMuted); },
                    ShowTooltip = () => MaterialText.SetTooltip(UiText.T(plugin.Configuration.UiLcarsSoundsMuted ? "UI sounds muted" : "UI sounds on")),
                };
                soundTitleButtons.Add(window, sound);
            }
            sound.Icon = plugin.Configuration.UiLcarsSoundsMuted ? FontAwesomeIcon.VolumeMute : FontAwesomeIcon.VolumeUp;
            if (theme.IsLcars && !window.TitleBarButtons.Contains(sound)) window.TitleBarButtons.Add(sound);
            else if (!theme.IsLcars) window.TitleBarButtons.Remove(sound);
        }
    }

    private void DrawTitleBarColour()
    {
        using var controls = MaterialControls.Push(HfhPresentation.Controls());
        if (DrawColourEditor("appearance")) plugin.SaveConfig();
    }

    private void DrawTransparencySettings()
    {
        var config = plugin.Configuration;
        var changed = false;
        var enabled = config.UiTransparencyEnabled;
        if (UiGui.Checkbox("Transparency###window-transparency", ref enabled))
        { config.UiTransparencyEnabled = enabled; changed = true; }
        ImGui.BeginDisabled(!config.UiTransparencyEnabled);
        try
        {
        ImGui.SetNextItemWidth(96 * AethertekUI.MaterialTheme.Metrics.Scale);
        var normal = config.UiWindowOpacityPercent;
        if (UiGui.InputInt("Opacity (%)###window-opacity", ref normal, 1, 100))
        { config.UiWindowOpacityPercent = normal; changed = true; }
        var autoFade = config.UiAutoFade;
        if (UiGui.Checkbox("Auto-fade when unfocused###window-auto-fade", ref autoFade))
        { config.UiAutoFade = autoFade; changed = true; }
        ImGui.BeginDisabled(!config.UiAutoFade);
        try
        {
        ImGui.SetNextItemWidth(96 * AethertekUI.MaterialTheme.Metrics.Scale);
        var faded = config.UiFadedOpacityPercent;
        if (UiGui.InputInt("Unfocused opacity (%)###window-faded-opacity", ref faded, 1, 100))
        { config.UiFadedOpacityPercent = faded; changed = true; }
        ImGui.SetNextItemWidth(96 * AethertekUI.MaterialTheme.Metrics.Scale);
        var delay = config.UiUnfocusedDelaySeconds;
        if (UiGui.InputInt("Unfocused delay (seconds)###window-unfocused-delay", ref delay, 1, 100))
        { config.UiUnfocusedDelaySeconds = delay; changed = true; }
        }
        finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndDisabled(); }
        if (changed) plugin.SaveConfig();
    }
}
