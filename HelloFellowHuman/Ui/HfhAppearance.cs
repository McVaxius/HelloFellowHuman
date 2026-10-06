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
    private readonly MaterialWindowFold fontStatusMotion = new();
    private readonly MaterialWindowDecorations fontStatusDecorations = new();
    private readonly MaterialOptions<string> languages = new(UiText.Languages.Select(l => new MaterialOption<string>(l.Code, l.Code, l.Name)).ToArray());
    private string appliedLanguage = "";
    private uint appliedAccent;
    private Vector3 accentDraft;
    private int checkedGeneration = -1;
    private bool fontIssueLogged;

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
            fontIssueLogged = false;
        }
        if (theme is null || appliedAccent != (plugin.Configuration.UiAccentRgb & 0xFFFFFF))
        {
            appliedAccent = plugin.Configuration.UiAccentRgb & 0xFFFFFF;
            theme = HfhPresentation.Theme(appliedAccent);
            var rgb = HfhPresentation.Rgb(appliedAccent);
            accentDraft = new(rgb.X, rgb.Y, rgb.Z);
        }
        theme.Density = plugin.Configuration.UiCompact ? MaterialDensity.Compact : MaterialDensity.Standard;
    }

    internal void Draw(WindowSystem windows)
    {
        Apply();
        if (!windows.Windows.Any(window => window.IsOpen)) return;
        using var resources = text.Enter();
        using var shaping = shapedText.Push();
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
                if (ImGui.Begin("Hello Fellow Human##FontStatus", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    fontStatusDecorations.Paint();
                    MaterialText.TextWrapped(UiText.T(fonts.LoadException is null && !fontIssueLogged ? "Loading UI fonts..." : "UI fonts failed to load. See the plugin log."));
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
        windows.Draw();
        foreach (var window in windows.Windows)
        {
            if (!windowOpacities.TryGetValue(window, out var opacity))
                windowOpacities.Add(window, opacity = new());
            ApplyWindowOpacity(opacity, window.WindowName);
        }
    }

    internal void DrawSelector()
    {
        var language = appliedLanguage;
        using var controls = MaterialControls.Push(HfhPresentation.Controls());
        var changed = MaterialAppearanceSelector.Draw("appearance", ref accentDraft, ref language, languages,
            new(UiText.T("Color"), UiText.T("Language"), UiText.T("Teal"), UiText.T("Blue"), UiText.T("Pink"), UiText.T("Custom RGB")), 140);
        if (changed.AccentChanged)
            plugin.Configuration.UiAccentRgb = ((uint)Math.Clamp((int)MathF.Round(accentDraft.X * 255), 0, 255) << 16)
                | ((uint)Math.Clamp((int)MathF.Round(accentDraft.Y * 255), 0, 255) << 8) | (uint)Math.Clamp((int)MathF.Round(accentDraft.Z * 255), 0, 255);
        if (changed.LanguageChanged) plugin.Configuration.UiLanguage = language;
        if (changed.AccentChanged || changed.LanguageChanged) plugin.SaveConfig();
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
        theme = HfhPresentation.Theme(appliedAccent);
        var rgb = HfhPresentation.Rgb(appliedAccent);
        accentDraft = new(rgb.X, rgb.Y, rgb.Z);
    }
    private IDisposable PushFont(UiFontRole role) => fonts.Push(role);

    public void Dispose() { shapedText.Dispose(); fonts?.Dispose(); text?.Dispose(); }

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
        var config = plugin.Configuration;
        var changed = false;
        var compactVisible = config.UiCompactVisibleOnMainWindow;
        if (UiGui.Checkbox("Compact visible on main window###window-compact-visible", ref compactVisible))
        { config.UiCompactVisibleOnMainWindow = compactVisible; changed = true; }
        var languageVisible = config.UiLanguageVisibleOnMainWindow;
        if (UiGui.Checkbox("Language visible on main window###window-language-visible", ref languageVisible))
        { config.UiLanguageVisibleOnMainWindow = languageVisible; changed = true; }
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
