using Dalamud.Bindings.ImGui;
using AethertekUI;
using AethertekUI.Dalamud;
using HelloFellowHuman.Ui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using HelloFellowHuman.Models;
using HelloFellowHuman.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace HelloFellowHuman.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly MaterialWindowMotion windowMotion = new();
    private readonly Configuration config;
    private readonly Plugin plugin;
    
    private int selectedPresetIndex = 0;
    private string newPresetName = string.Empty;
    private string importText = string.Empty;
    private readonly Dictionary<int, string> emoteSearchFilters = new();
    private bool hasLoggedWindowLoad = false;
    private bool configurationTabRequested;
    
    // Color picker state management
    private readonly Dictionary<string, Vector3> editingColors = new();
    
    public ConfigWindow(Plugin plugin) : base($"Hello Fellow Human Config v{Assembly.GetExecutingAssembly().GetName().Version}###HFHConfig")
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;
        Flags |= ImGuiWindowFlags.HorizontalScrollbar;
        
        Size = new Vector2(1460, 930);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        selectedPresetIndex = 0;
        TitleBarButtons.Add(new()
        {
            Icon = FontAwesomeIcon.Cog, Priority = 0, IconOffset = new(2, 1),
            Click = button =>
            {
                if (button != ImGuiMouseButton.Left) return;
                configurationTabRequested = true;
                ImGui.SetWindowCollapsed(WindowName, false);
            },
            ShowTooltip = () => UiGui.SetTooltip("Configuration"),
        });
        TitleBarButtons.Add(new()
        {
            Icon = FontAwesomeIcon.Wrench, Priority = -10, IconOffset = new(2, 1),
            Click = button => { if (button == ImGuiMouseButton.Left) plugin.OpenSetupWizard(SetupWizardMode.Setup, selectedPresetIndex); },
            ShowTooltip = () => UiGui.SetTooltip("Create a reaction with a plain-language guided wizard"),
        });
        TitleBarButtons.Add(new()
        {
            Icon = FontAwesomeIcon.PowerOff, Priority = -20, IconOffset = new(2, 1),
            Click = button =>
            {
                if (button == ImGuiMouseButton.Left && plugin.ConfigManager.GetCurrentAccount() is { } account)
                    SetAccountEnabled(account, !account.Enabled);
            },
            ShowTooltip = () => MaterialText.SetTooltip(plugin.ConfigManager.GetCurrentAccount() is { } account
                ? UiText.T("Enabled") + ": " + UiText.T(account.Enabled ? "Yes" : "No")
                : UiText.T("Log in and select an account before editing presets.")),
        });
    }
    
    public override void PreDraw() => windowMotion.Prepare(this, reducedMotion: false, roundedCorners: true);

    public override void PostDraw()
    {
        windowMotion.Restore(this);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        UiGui.ImageTitle(this, "Hello Fellow Human — " + UiText.T("Configuration") + $" v{version}", plugin.OriginalIcon);
    }

    public override void Draw()
    {
        windowMotion.DrawChrome();
        // Log window load once
        if (!hasLoggedWindowLoad)
        {
            Plugin.Log.Debug("[HFH] Config window opened - NEW DLL v2.0");
            hasLoggedWindowLoad = true;
        }
        
        // UNIQUE MARKER: This should appear in logs if new DLL is loaded
        // Plugin.Log.Debug("[HFH] NEW DLL LOADED - If you see this, the fix is working");
        
        DrawHeader();
        statusRowOrigin = ImGui.GetCursorScreenPos();
        var scale = MaterialTheme.Metrics.Scale;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(48, HfhPresentation.Compact ? 10 : 14) * scale);
        bool tabsOpen;
        using (MaterialText.PushLineHeight(UiText.T("Presets"), UiText.T("Configuration")))
            tabsOpen = ImGui.BeginTabBar("HFHTabBar", ImGuiTabBarFlags.FittingPolicyScroll);
        ImGui.PopStyleVar();
        if (tabsOpen)
        {
            bool presetsOpen = true;
            if (UiGui.BeginTabItem("Presets", ref presetsOpen, ImGuiTabItemFlags.None))
            {
                DrawPresetsTab();
                ImGui.EndTabItem();
            }
            
            bool configOpen = true;
            var configurationFlags = configurationTabRequested ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            configurationTabRequested = false;
            if (UiGui.BeginTabItem("Configuration", ref configOpen, configurationFlags))
            {
                DrawConfigurationTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    private Vector2 statusRowOrigin;

    private void DrawHeader()
    {
        var scale = MaterialTheme.Metrics.Scale;
        var compact = HfhPresentation.Compact;
        var start = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var icon = plugin.OriginalIcon;
        MaterialCanvas.DrawImage(ImGui.GetWindowDrawList(), icon.Handle, icon.Size,
            start, start + new Vector2((compact ? 44 : 52) * scale));
        ImGui.SetCursorScreenPos(start + new Vector2(compact ? 62 : 76, 0) * scale);
        using (UiText.Font(compact ? UiFontRole.CompactTitle : UiFontRole.Title)) MaterialText.Text("Hello Fellow Human");
        var titleMax = ImGui.GetItemRectMax();
        ImGui.SetCursorScreenPos(start + new Vector2(compact ? 62 : 76, compact ? 34 : 39) * scale);
        UiGui.TextColored(MaterialTheme.Current.Colors.OnSurfaceVariant, "A friendlier Eorzea, one emote at a time.");
        var subtitleMax = ImGui.GetItemRectMax();
        var supportWidth = UiGui.ButtonWidth("Support on Ko-fi", MaterialIcon.Heart);
        var setupWidth = UiGui.ButtonWidth("Guided Setup", MaterialIcon.Document);
        var toolsWidth = supportWidth + setupWidth + (config.UiLanguageVisibleOnMainWindow ? plugin.Appearance.LanguageWidth() : 0)
            + (config.UiCompactVisibleOnMainWindow ? UiGui.CheckboxWidth("C") : 0) + UiGui.CheckboxWidth("Transparency") + 4 * ImGui.GetStyle().ItemSpacing.X;
        var brandWidth = Math.Max(titleMax.X, subtitleMax.X) - start.X;
        var right = width >= toolsWidth + brandWidth + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorScreenPos(right ? start + new Vector2(width - toolsWidth, 5 * scale)
            : new Vector2(start.X, Math.Max(start.Y + (compact ? 61 : 72) * scale, subtitleMax.Y + ImGui.GetStyle().ItemSpacing.Y)));
        ImGui.BeginGroup();
        if (UiGui.Button("\u2661 Ko-fi \u2661", UiText.T("Support on Ko-fi"), MaterialIcon.Heart))
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/mcvaxius", UseShellExecute = true });
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Support development on Ko-fi");
        if (config.UiLanguageVisibleOnMainWindow)
        { UiGui.SameLineIfFits(plugin.Appearance.LanguageWidth()); plugin.Appearance.DrawLanguage(); }
        UiGui.SameLineIfFits(setupWidth);
        if (UiGui.Button("Guided Setup", icon: MaterialIcon.Document)) plugin.OpenSetupWizard(SetupWizardMode.Setup, selectedPresetIndex);
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Create a reaction with a plain-language guided wizard");
        if (config.UiCompactVisibleOnMainWindow)
        {
            UiGui.SameLineIfFits(UiGui.CheckboxWidth("C"));
            var density = config.UiCompact;
            if (ImGui.Checkbox("C", ref density)) { config.UiCompact = density; plugin.SaveConfig(); }
            if (ImGui.IsItemHovered()) UiGui.SetTooltip("Compact mode");
        }
        UiGui.SameLineIfFits(UiGui.CheckboxWidth("Transparency"));
        plugin.Appearance.DrawTransparencyToggle();
        ImGui.EndGroup();
        var bottom = Math.Max(start.Y + (right ? HfhPresentation.HeaderHeight : compact ? 105 : 122) * scale, ImGui.GetItemRectMax().Y + HfhPresentation.Gap * scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, bottom));
        ImGui.Separator();
    }

    private bool DrawStatusControls()
    {
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null) { UiGui.TextWrapped("Log in and select an account before editing presets."); return false; }
        var body = ImGui.GetCursorScreenPos();
        var total = UiGui.CheckboxWidth("Enabled") + UiGui.CheckboxWidth("DTR ON") + UiGui.CheckboxWidth("Krangle") + 3 * ImGui.GetStyle().ItemSpacing.X;
        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var onTabs = rightEdge - total > statusRowOrigin.X + 450 * MaterialTheme.Metrics.Scale;
        if (onTabs) ImGui.SetCursorScreenPos(new Vector2(rightEdge - total, statusRowOrigin.Y));
        var enabled = account.Enabled;
        if (UiGui.Checkbox("Enabled", ref enabled))
            SetAccountEnabled(account, enabled);
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Enable/disable the plugin's emote automation");
        UiGui.SameLineIfFits(UiGui.CheckboxWidth("DTR ON"));
        var dtr = config.DtrBarEnabled;
        if (UiGui.Checkbox("DTR ON", ref dtr)) { config.DtrBarEnabled = dtr; plugin.SaveConfig(); }
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Show/hide the DTR bar entry (server info bar)");
        UiGui.SameLineIfFits(UiGui.CheckboxWidth("Krangle"));
        var krangle = config.KrangleEnabled;
        if (UiGui.Checkbox("Krangle", ref krangle)) { config.KrangleEnabled = krangle; plugin.SaveConfig(); KrangleService.ClearCache(); }
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Obfuscate player names with military/exercise words.\nUseful for screenshots.");
        if (onTabs) ImGui.SetCursorScreenPos(body);
        else ImGui.Spacing();
        return true;
    }

    private void SetAccountEnabled(AccountConfig account, bool enabled)
    {
        account.Enabled = enabled;
        plugin.ConfigManager.SaveCurrentAccount();
        Plugin.Log.Info($"Hello Fellow Human {(enabled ? "enabled" : "disabled")}");
    }

    private void DrawConfigurationTab()
    {
        if (!DrawStatusControls()) return;
        UiGui.Text("Window appearance");
        ImGui.Separator();
        ImGui.PushID("settings-appearance");
        plugin.Appearance.DrawSelector();
        ImGui.PopID();
        var density = config.UiCompact;
        if (UiGui.Checkbox("Compact mode", ref density)) { config.UiCompact = density; plugin.SaveConfig(); }
        plugin.Appearance.DrawWindowSettings();
        ImGui.Separator();
        var dtrMode = config.DtrBarMode;
        var dtrModes = new[] { "Text Only", "Icon+Text", "Icon Only" };
        ImGui.SetNextItemWidth(120);
        if (UiGui.Combo("DTR Mode", ref dtrMode, dtrModes, dtrModes.Length))
        {
            config.DtrBarMode = dtrMode;
            plugin.SaveConfig();
        }
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("DTR bar display mode:\nText Only: 'HFH: ON/OFF [preset]'\nIcon+Text: '⚫ HFH'\nIcon Only: '⚫'");
        
        ImGui.Spacing();
        UiGui.Text("DTR Icons (max 3 characters)");
        ImGui.SameLine();
        HelpMarker("Customize the glyphs shown in icon modes when HFH is enabled/disabled.");
        ImGui.SameLine();
        if (UiGui.SmallButton("Copy Icon Guide Link##hfh"))
        {
            ImGui.SetClipboardText(IconGuideUrl);
            Plugin.Log.Info("Copied icon guide link to clipboard");
        }
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Copies the Lodestone blog link with suggested glyphs");

        var enabledIcon = config.DtrIconEnabled;
        if (DrawIconInputs("Enabled", ref enabledIcon, "\uE03C"))
        {
            config.DtrIconEnabled = enabledIcon;
            plugin.SaveConfig();
        }

        var disabledIcon = config.DtrIconDisabled;
        if (DrawIconInputs("Disabled", ref disabledIcon, "\uE03D"))
        {
            config.DtrIconDisabled = disabledIcon;
            plugin.SaveConfig();
        }

    }

    private void DrawPresetsTab()
    {
        if (!DrawStatusControls()) return;
        var scale = MaterialTheme.Metrics.Scale;
        var width = ImGui.GetContentRegionAvail().X;
        var sideBySide = width >= 680 * scale;
        var leftWidth = sideBySide ? Math.Clamp(width * .20f, 230 * scale, 310 * scale) : width;
        var gap = HfhPresentation.Gap * scale;
        var height = Math.Max(220 * scale, ImGui.GetContentRegionAvail().Y - gap);
        var leftSize = new Vector2(leftWidth, sideBySide ? height : 220 * scale);
        var origin = ImGui.GetCursorScreenPos();
        HfhPresentation.Surface(origin, origin + leftSize);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(HfhPresentation.Compact ? 10 : 14) * scale);
        if (ImGui.BeginChild("PresetList", leftSize, true, ImGuiWindowFlags.HorizontalScrollbar)) DrawPresetList();
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        if (sideBySide) ImGui.SameLine(0, gap);
        var editorSize = new Vector2(sideBySide ? width - leftWidth - gap : width, sideBySide ? height : 460 * scale);
        origin = ImGui.GetCursorScreenPos();
        HfhPresentation.Surface(origin, origin + editorSize);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("PresetEditor", editorSize, true, ImGuiWindowFlags.HorizontalScrollbar)) DrawPresetEditor();
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawPresetList()
    {
        using (UiText.Font(HfhPresentation.Compact ? UiFontRole.CompactPaneHeading : UiFontRole.PaneHeading)) UiGui.Text("Presets");
        ImGui.Separator();
        
        var acctList = plugin.ConfigManager.GetOrCreateCurrentAccount();
        for (int i = 0; i < acctList.Presets.Count; i++)
        {
            var preset = acctList.Presets[i];
            var isSelected = i == selectedPresetIndex;
            var isActive = i == acctList.SelectedPresetIndex;

            var presetName = config.KrangleEnabled ? KrangleService.KrangleName(preset.Name) : preset.Name;
            var displayName = $"[{i}] {presetName}";
            if (isActive)
                displayName += " (ACTIVE)";

            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 1.0f, 0.2f, 1));

            var origin = ImGui.GetCursorScreenPos();
            var scale = MaterialTheme.Metrics.Scale;
            var height = (HfhPresentation.Compact ? 60 : 68) * scale;
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            var clicked = ImGui.Selectable(displayName, isSelected, ImGuiSelectableFlags.None, new Vector2(0, height));
            ImGui.PopStyleColor();
            var max = ImGui.GetItemRectMax(); var colors = MaterialTheme.Current.Colors; var dl = ImGui.GetWindowDrawList();
            var detail = UiText.T(i == 0 ? "Built-in preset" : "User preset") + (isActive ? " · " + UiText.T("ACTIVE") : "");
            dl.PushClipRect(origin, max, true);
            try
            {
            if (isSelected) dl.AddRect(origin, max, MaterialCanvas.Color(colors.Primary), 4 * scale);
            if (isSelected) dl.AddRectFilled(origin, new Vector2(origin.X + 5 * scale, max.Y), MaterialCanvas.Color(colors.Primary));
            using (UiText.Font(UiFontRole.BodyStrong)) MaterialText.AddText(dl,ImGui.GetFont(), ImGui.GetFontSize(), origin + new Vector2(12, 9) * scale, MaterialCanvas.Color(colors.OnSurface), presetName);
            MaterialText.AddText(dl,origin + new Vector2(12, 36) * scale, MaterialCanvas.Color(isActive ? new Vector4(.2f, 1, .2f, 1) : colors.OnSurfaceVariant), detail);
            }
            finally { dl.PopClipRect(); }
            if (ImGui.IsItemHovered()) MaterialText.SetTooltip(presetName + "\n" + detail);
            if (clicked)
            {
                var oldActiveIndex = acctList.SelectedPresetIndex;
                selectedPresetIndex = i;
                acctList.SelectedPresetIndex = i;

                Plugin.Log.Debug($"[HFH] Preset changed to {i}: {preset.Name}");

                // Clear editing dictionaries when switching presets to prevent conflicts
                editingColors.Clear();

                // Reset cooldowns when switching active presets
                if (oldActiveIndex != i)
                {
                    var newPreset = acctList.Presets[i];
                    foreach (var line in newPreset.Lines)
                    {
                        line.ResetRuntimeState();
                    }
                    Plugin.Log.Info($"[HFH] Switched to preset '{newPreset.Name}', cooldowns reset");
                }

                plugin.ConfigManager.SaveCurrentAccount();
            }

            if (isActive)
                ImGui.PopStyleColor();
        }
        var spacing = ImGui.GetStyle().ItemSpacing;
        var visibleWidth = ImGuiP.GetCurrentWindow().InnerRect.Max.X - ImGui.GetStyle().WindowPadding.X - ImGui.GetCursorScreenPos().X;
        var newWidth = MaterialText.Measure(UiText.T("+ New Preset")).X + 2 * ImGui.GetStyle().FramePadding.X;
        var actionRows = newWidth + UiGui.ButtonWidth("Delete", MaterialIcon.Delete) + spacing.X <= visibleWidth ? 1 : 2;
        var hintHeight = MaterialText.Measure(UiText.T("Hold CTRL to delete"), false, Math.Max(1, visibleWidth)).Y;
        var footer = actionRows * (ImGui.GetFrameHeight() + spacing.Y) + hintHeight + spacing.Y + ImGui.GetStyle().WindowPadding.Y + 1;
        ImGui.SetCursorPosY(Math.Max(ImGui.GetCursorPosY(), ImGui.GetWindowHeight() - footer));
        using (var action = new MaterialStyleScope())
        {
            action.Color(ImGuiCol.Button, MaterialTheme.Current.Colors.Primary);
            action.Color(ImGuiCol.Text, MaterialTheme.Current.Colors.OnPrimary);
            if (UiGui.Button("New", UiText.T("+ New Preset"))) ImGui.OpenPopup("NewPresetPopup");
        }
        
        ImGui.SetNextWindowSize(new Vector2(360 * MaterialTheme.Metrics.Scale, 0));
        if (ImGui.BeginPopup("NewPresetPopup"))
        {
            UiGui.Text("Enter preset name:");
            ImGui.SetNextItemWidth(-1);
            UiGui.InputText("##newpreset", ref newPresetName, 100);
            
            if (UiGui.Button("Create"))
            {
                if (!string.IsNullOrWhiteSpace(newPresetName))
                {
                    var acct = plugin.ConfigManager.GetOrCreateCurrentAccount();
                    var defaultPreset = acct.Presets.Count > 0 ? acct.Presets[0].Clone() : new EmotePreset { Name = newPresetName };
                    defaultPreset.Name = newPresetName;
                    acct.Presets.Add(defaultPreset);
                    selectedPresetIndex = acct.Presets.Count - 1;
                    acct.SelectedPresetIndex = selectedPresetIndex;
                    plugin.ConfigManager.SaveCurrentAccount();
                    newPresetName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.SameLine();
            if (UiGui.Button("Cancel"))
            {
                newPresetName = string.Empty;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        
        UiGui.SameLineIfFits(UiGui.ButtonWidth("Delete", MaterialIcon.Delete));
        var ctrlHeld = ImGui.GetIO().KeyCtrl;
        var deleteButtonColor = ctrlHeld ? new Vector4(1, 0, 0, 1) : MaterialTheme.Current.Colors.SurfaceContainerHigh;
        ImGui.PushStyleColor(ImGuiCol.Button, deleteButtonColor);
        
        if (UiGui.Button("Delete", icon: MaterialIcon.Delete))
        {
            var acct = plugin.ConfigManager.GetOrCreateCurrentAccount();
            if (ctrlHeld && selectedPresetIndex > 0 && acct.Presets[selectedPresetIndex].Name != "DEFAULT PRESET")
            {
                acct.Presets.RemoveAt(selectedPresetIndex);
                selectedPresetIndex = Math.Min(selectedPresetIndex, acct.Presets.Count - 1);
                acct.SelectedPresetIndex = selectedPresetIndex;
                plugin.ConfigManager.SaveCurrentAccount();
            }
        }
        ImGui.PopStyleColor();
        
        if (!ctrlHeld)
        {
            UiGui.TextColored(MaterialTheme.Current.Colors.OnSurfaceVariant, "Hold CTRL to delete");
        }
        
        ImGui.Separator();
        
    }

    private void DrawPresetEditor()
    {
        var account = plugin.ConfigManager.GetOrCreateCurrentAccount();
        if (selectedPresetIndex < 0 || selectedPresetIndex >= account.Presets.Count)
            return;
        
        var preset = account.Presets[selectedPresetIndex];
        
        var editingName = config.KrangleEnabled ? KrangleService.KrangleName(preset.Name) : preset.Name;
        var headerOrigin = ImGui.GetCursorScreenPos();
        var headerWidth = ImGui.GetContentRegionAvail().X;
        var actionsWidth = UiGui.ButtonWidth("Export", MaterialIcon.ExternalLink) + UiGui.ButtonWidth("Import", MaterialIcon.Download) + ImGui.GetStyle().ItemSpacing.X;
        if (preset.Name == "DEFAULT PRESET") actionsWidth += UiGui.ButtonWidth("Reset Default") + ImGui.GetStyle().ItemSpacing.X;
        var nameWidth = MaterialText.Measure(UiText.T("Preset Name")).X + ImGui.GetStyle().ItemSpacing.X;
        var inlineActions = headerWidth >= nameWidth + 120 * MaterialTheme.Metrics.Scale + actionsWidth + 2 * ImGui.GetStyle().ItemSpacing.X;
        UiGui.TextUnformatted("Preset Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Min(372 * MaterialTheme.Metrics.Scale, inlineActions ? headerWidth - nameWidth - actionsWidth - 2 * ImGui.GetStyle().ItemSpacing.X : ImGui.GetContentRegionAvail().X));
        UiGui.InputText("##presetNameDisplay", ref editingName, 200, ImGuiInputTextFlags.ReadOnly);
        var nameBottom = ImGui.GetCursorScreenPos().Y;
        if (inlineActions) ImGui.SetCursorScreenPos(headerOrigin + new Vector2(headerWidth - actionsWidth, 0));
        else ImGui.Spacing();
        
        if (UiGui.Button("Export", icon: MaterialIcon.ExternalLink))
        {
            var base64 = preset.ToBase64();
            ImGui.SetClipboardText(base64);
            Plugin.Log.Info("Preset exported to clipboard");
        }
        
        UiGui.SameLineIfFits(UiGui.ButtonWidth("Import", MaterialIcon.Download));
        
        if (UiGui.Button("Import", icon: MaterialIcon.Download))
        {
            var clipboardText = ImGui.GetClipboardText();
            var imported = EmotePreset.FromBase64(clipboardText);
            if (imported != null)
            {
                preset.Lines = imported.Lines;
                plugin.ConfigManager.SaveCurrentAccount();
                Plugin.Log.Info("Preset imported successfully");
            }
            else
            {
                Plugin.Log.Error("Failed to import preset - invalid clipboard data");
            }
        }
        
        if (preset.Name == "DEFAULT PRESET")
        {
            UiGui.SameLineIfFits(UiGui.ButtonWidth("Reset Default"));
            if (UiGui.Button("Reset Default"))
            {
                preset.Lines.Clear();
                preset.Lines.Add(new EmoteLine
                {
                    TargetName = "Example Player",
                    SlashCommand = "/wave",
                    WaitTimeAfter = 3.0f,
                    RepeatInterval = 5.0f,
                    DistanceThreshold = 5.0f
                });
                plugin.ConfigManager.SaveCurrentAccount();
            }
        }
        
        if (inlineActions) ImGui.SetCursorScreenPos(new Vector2(headerOrigin.X, Math.Max(nameBottom, ImGui.GetCursorScreenPos().Y)));
        // The built-in preset remains editable; do not claim another preset is active.
        if (selectedPresetIndex == 0) UiGui.TextColored(new Vector4(1, .6f, .3f, 1), "Editing the built-in preset.");
        ImGui.Separator();
        using (UiText.Font(HfhPresentation.Compact ? UiFontRole.CompactPaneHeading : UiFontRole.PaneHeading)) UiGui.Text("Rules");
        UiGui.TextColored(MaterialTheme.Current.Colors.OnSurfaceVariant, "Define when and how to perform social interactions.");
        ImGui.Separator();
        
        var editorRoot = ImGuiP.GetCurrentWindow().ID;
        var scale = MaterialTheme.Metrics.Scale;
        var gridHeight = Math.Max(110 * scale, ImGui.GetContentRegionAvail().Y - (HfhPresentation.Compact ? 64 : 84) * scale);
        ImGui.SetNextWindowContentSize(new Vector2(Math.Max(1775 * scale, RuleInitialWidths().Sum() + 130 * scale), 0));
        if (ImGui.BeginChild("##HFHRuleGrid", new Vector2(-1, gridHeight), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            ImGuiP.PushOverrideID(editorRoot);
            try { DrawRules(preset); }
            finally { ImGui.PopID(); }
        }
        ImGui.EndChild();
        ImGui.Spacing();
        if (UiGui.Button("+ Add Blank Rule"))
        {
            preset.Lines.Add(new EmoteLine
            {
                TargetName = "",
                SlashCommand = "",
                WaitTimeAfter = 3.0f,
                RepeatInterval = 5.0f,
                DistanceThreshold = 5.0f,
                WeatherFilter = "ALL",
                EmoteRange = 10.0f
            });
            plugin.ConfigManager.SaveCurrentAccount();
        }

        UiGui.SameLineIfFits(UiGui.ButtonWidth("Add Rule with Wizard", MaterialIcon.Star));
        using (var action = new MaterialStyleScope())
        {
            action.Color(ImGuiCol.Button, MaterialTheme.Current.Colors.Primary);
            action.Color(ImGuiCol.Text, MaterialTheme.Current.Colors.OnPrimary);
            if (UiGui.Button("Add Rule with Wizard", icon: MaterialIcon.Star)) plugin.OpenSetupWizard(SetupWizardMode.AddRule, selectedPresetIndex);
        }
    }

    private void DrawRules(EmotePreset preset)
    {
        ImGui.Columns(13, "EmoteColumns");
        var state = ImGui.GetStateStorage(); var initialized = ImGui.GetID("##HFHColumnsInitialized");
        if (!state.GetBool(initialized))
        {
            var widths = RuleInitialWidths();
            for (var column = 0; column < widths.Length; column++) ImGui.SetColumnWidth(column, widths[column]);
            state.SetBool(initialized, true);
        }
        var minimumWidths = RuleMinimumWidths();
        for (var column = 0; column < minimumWidths.Length; column++)
            if (ImGui.GetColumnWidth(column) < minimumWidths[column]) ImGui.SetColumnWidth(column, minimumWidths[column]);
        UiGui.Text("Type");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Proximity = distance-based, Emote = responds to emotes directed at you");
        ImGui.NextColumn();
        UiGui.Text("ALL");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Check to target all nearby players");
        ImGui.NextColumn();
        
        UiGui.Text("ToT");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Target triggering player: if checked, /target the player before executing the slash command");
        ImGui.NextColumn();
        
        UiGui.Text("Name");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Target player name (without @server). For Emote type, leave blank to respond to anyone.");
        ImGui.NextColumn();
        UiGui.Text("Command");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Slash command to execute. Try '/wave motion' to emote without text!");
        ImGui.NextColumn();
        UiGui.Text("Wait");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Seconds to wait after executing this emote");
        ImGui.NextColumn();
        UiGui.Text("Repeat");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Seconds before this emote can trigger again (Proximity only)");
        ImGui.NextColumn();
        UiGui.Text("Dist/Emote");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Proximity: max distance (yalms). Emote: the trigger emote slash command.\nCOPYCAT: Responds with ANY emote received (copies the emote).");
        ImGui.NextColumn();
        UiGui.Text("Weather");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Weather condition required for this line to trigger (ALL = any weather)");
        ImGui.NextColumn();
        UiGui.Text("Emote Range");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Range for emote triggers in yalms (default: 10). Only applies to Emote type lines.");
        ImGui.NextColumn();
        
        // Glow animation columns
        UiGui.Text("Glow");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Enable glow animation with color effect");
        ImGui.NextColumn();
        
        UiGui.Text("Color");
        if (ImGui.IsItemHovered())
            UiGui.SetTooltip("Glow color (RGB)");
        ImGui.NextColumn();
        
        UiGui.Text("Remove");
        ImGui.NextColumn(); // Delete button column
        ImGui.Separator();
        
        for (int i = 0; i < preset.Lines.Count; i++)
        {
            var line = preset.Lines[i];
            var isValid = line.IsValid();
            
            if (!isValid)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
            
            // Type dropdown column
            var triggerType = line.TriggerType;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.Combo($"##type{i}", ref triggerType, "Proximity\0Emote\0"))
            {
                line.TriggerType = triggerType;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            // ALL checkbox column - now editable for all types
            var isEmoteType = line.TriggerType == 1;
            var isAllTargets = line.TargetName == "*";
            if (UiGui.Checkbox($"##all{i}", ref isAllTargets))
            {
                line.TargetName = isAllTargets ? "*" : "";
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            // Target before command checkbox
            var targetBefore = line.TargetBeforeCommand;
            if (UiGui.Checkbox($"##tgt{i}", ref targetBefore))
            {
                line.TargetBeforeCommand = targetBefore;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            var name = line.TargetName;
            var displayName = config.KrangleEnabled && name != "*" && !string.IsNullOrWhiteSpace(name)
                ? KrangleService.KrangleName(name) : name;
            var nameWidth = Math.Max(80 * MaterialTheme.Metrics.Scale, Math.Min(220 * MaterialTheme.Metrics.Scale,
                ImGui.GetContentRegionAvail().X - UiGui.ButtonWidth("K") - ImGui.GetStyle().ItemSpacing.X));
            ImGui.SetNextItemWidth(nameWidth);
            
            // Name field logic:
            // - ALL targets: readonly (shows "*")
            // - Specific targets: editable
            var inputFlags = isAllTargets ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
            
            // Apply krangle readonly only for non-ALL, non-empty names
            if (config.KrangleEnabled && name != "*" && !string.IsNullOrWhiteSpace(name))
            {
                inputFlags |= ImGuiInputTextFlags.ReadOnly;
            }
            
            if (config.KrangleEnabled && name != "*" && !string.IsNullOrWhiteSpace(name))
            {
                UiGui.InputText($"##name{i}", ref displayName, 100, inputFlags);
            }
            else
            {
                if (UiGui.InputText($"##name{i}", ref displayName, 100, inputFlags))
                {
                    if (!isAllTargets)
                    {
                        line.TargetName = displayName;
                        plugin.ConfigManager.SaveCurrentAccount();
                    }
                }
            }
            ImGui.SameLine();
            if (UiGui.SmallButton($"K##kr{i}"))
            {
                if (name != "*" && !string.IsNullOrWhiteSpace(name))
                {
                    line.TargetName = KrangleService.KrangleName(name);
                    plugin.ConfigManager.SaveCurrentAccount();
                }
            }
            if (ImGui.IsItemHovered())
                UiGui.SetTooltip("Krangle this name (permanent obfuscation)");
            ImGui.NextColumn();
            
            var cmd = line.SlashCommand;
            ImGui.SetNextItemWidth(220);
            
            // Show editable command field for COPYCAT with default *
            if (line.TriggerEmote == "COPYCAT")
            {
                var fallbackCmd = line.SlashCommand;
                if (string.IsNullOrEmpty(fallbackCmd)) fallbackCmd = "*"; // Default fallback
                ImGui.SetNextItemWidth(220);
                if (UiGui.InputText($"##cmd{i}", ref fallbackCmd, 100))
                {
                    line.SlashCommand = fallbackCmd;
                    plugin.ConfigManager.SaveCurrentAccount();
                }
                if (ImGui.IsItemHovered())
                    UiGui.SetTooltip("COPYCAT mode - fallback command when emote copying fails or the incoming emote is already looping.\nUse media:, video:, audio:, or sound: to launch a local file.");
            }
            else
            {
                if (UiGui.InputText($"##cmd{i}", ref cmd, 100))
                {
                    line.SlashCommand = cmd;
                    plugin.ConfigManager.SaveCurrentAccount();
                }
            }
            if (ImGui.IsItemHovered() && line.TriggerEmote != "COPYCAT")
                UiGui.SetTooltip("Slash command to execute.\nUse media:, video:, audio:, or sound: to launch a local file relative to the plugin config folder or by full path.");
            ImGui.NextColumn();
            
            var wait = line.WaitTimeAfter;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.DragFloat($"##wait{i}", ref wait, 0.1f, 0f, 60f, "%.1f"))
            {
                line.WaitTimeAfter = wait;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            // Repeat interval is editable for both proximity and emote types
            var repeat = line.RepeatInterval;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.DragFloat($"##repeat{i}", ref repeat, 0.1f, 0.1f, 300f, "%.1f"))
            {
                line.RepeatInterval = repeat;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            if (isEmoteType)
            {
                // Emote type: show emote selector dropdown with search
                var emoteCommands = plugin.EmoteDetectionService.EmoteCommands;
                var triggerEmote = line.TriggerEmote;
                ImGui.SetNextItemWidth(-1);
                if (UiGui.BeginCombo($"##emote{i}", string.IsNullOrEmpty(triggerEmote) ? "(select)" : triggerEmote))
                {
                    if (!emoteSearchFilters.ContainsKey(i))
                        emoteSearchFilters[i] = "";
                    var filter = emoteSearchFilters[i];
                    ImGui.SetNextItemWidth(-1);
                    if (UiGui.InputText($"##efilter{i}", ref filter, 64))
                        emoteSearchFilters[i] = filter;
                    
                    var filterLower = filter.ToLowerInvariant();
                    var shown = 0;
                    foreach (var ec in emoteCommands)
                    {
                        if (!string.IsNullOrEmpty(filterLower) && !ec.ToLowerInvariant().Contains(filterLower))
                            continue;
                        
                        var isSelected = ec == triggerEmote;
                        if (MaterialText.Selectable(ec, isSelected))
                        {
                            line.TriggerEmote = ec;
                            plugin.ConfigManager.SaveCurrentAccount();
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                        
                        if (++shown > 50) break; // Limit visible items
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    UiGui.SetTooltip("The emote that triggers this response (e.g. /wave)");
            }
            else
            {
                // Proximity type: show distance
                var dist = line.DistanceThreshold;
                ImGui.SetNextItemWidth(-1);
                if (UiGui.DragFloat($"##dist{i}", ref dist, 0.1f, 0.1f, 100f, "%.1f"))
                {
                    line.DistanceThreshold = dist;
                    plugin.ConfigManager.SaveCurrentAccount();
                }
            }
            ImGui.NextColumn();
            
            // Weather column
            var weatherTypes = WeatherService.GetWeatherTypes();
            var currentWeatherIndex = weatherTypes.IndexOf(line.WeatherFilter);
            if (currentWeatherIndex == -1) currentWeatherIndex = 0; // Default to ALL
            
            ImGui.SetNextItemWidth(-1);
            if (UiGui.Combo($"##weather{i}", ref currentWeatherIndex, weatherTypes.ToArray(), weatherTypes.Count))
            {
                line.WeatherFilter = weatherTypes[currentWeatherIndex];
                plugin.ConfigManager.SaveCurrentAccount();
            }
            if (ImGui.IsItemHovered())
                UiGui.SetTooltip("Select weather condition for this line");
            ImGui.NextColumn();
            
            // Emote Range column (only for emote type lines)
            if (line.TriggerType == 1) // Emote type
            {
                var emoteRange = line.EmoteRange;
                ImGui.SetNextItemWidth(-1);
                if (UiGui.DragFloat($"##emoteRange{i}", ref emoteRange, 0.1f, 0.1f, 100f, "%.1f"))
                {
                    line.EmoteRange = emoteRange;
                    plugin.ConfigManager.SaveCurrentAccount();
                }
                if (ImGui.IsItemHovered())
                    UiGui.SetTooltip("Range for emote triggers in yalms");
            }
            else
            {
                // Show empty for proximity type
                UiGui.TextDisabled("--");
                if (ImGui.IsItemHovered())
                    UiGui.SetTooltip("Emote range only applies to Emote type lines");
            }
            ImGui.NextColumn();
            
            // Glow animation checkbox
            var glowEnabled = line.GlowEnabled;
            if (UiGui.Checkbox($"##glow{i}", ref glowEnabled))
            {
                line.GlowEnabled = glowEnabled;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            // Color picker (only enabled if glow is enabled)
            var glowColor = line.GlowColor;
            var colorId = $"##color_preset{selectedPresetIndex}_line{i}";
            
            var modifiedColor = DrawColorPicker(colorId, ref glowColor, readOnly: !line.GlowEnabled);
            if (modifiedColor)
            {
                Plugin.Log.Debug($"[HFH] Color changed for preset {selectedPresetIndex}, line {i}");
                line.GlowColor = glowColor;
                plugin.ConfigManager.SaveCurrentAccount();
            }
            ImGui.NextColumn();
            
            if (i > 0 || preset.Lines.Count > 1)
            {
                if (UiGui.Button($"-##del{i}"))
                {
                    preset.Lines.RemoveAt(i);
                    plugin.ConfigManager.SaveCurrentAccount();
                    if (!isValid) ImGui.PopStyleColor();
                    break;
                }
            }
            
            ImGui.NextColumn();
            
            if (!isValid)
                ImGui.PopStyleColor();
        }
        
        ImGui.Columns(1);
        ImGui.Separator();
        
    }

    public void Dispose()
    {
    }

    private static float[] RuleInitialWidths()
    {
        float[] defaults = [115, 55, 55, 275, 235, 90, 95, 150, 145, 115, 65, 160, 80];
        var minimums = RuleMinimumWidths();
        var scale = MaterialTheme.Metrics.Scale;
        return defaults.Select((width, column) => Math.Max(width * scale, minimums[column])).ToArray();
    }

    private static float[] RuleMinimumWidths()
    {
        var style = ImGui.GetStyle(); var scale = MaterialTheme.Metrics.Scale;
        var labels = new[] { "Type", "ALL", "ToT", "Name", "Command", "Wait", "Repeat", "Dist/Emote", "Weather", "Emote Range", "Glow", "Color", "Remove" };
        var widths = labels.Select(label => MaterialText.Measure(UiText.T(label)).X + 2 * style.ItemSpacing.X).ToArray();
        float ComboWidth(IEnumerable<string> options) => options.Max(option => MaterialText.Measure(UiText.T(option)).X) + ImGui.GetFrameHeight() + 2 * style.FramePadding.X + 2 * style.ItemSpacing.X;
        var field = MathF.Ceiling(Math.Max(80 * scale, MaterialText.Measure("-00000.0").X + 2 * style.FramePadding.X)) + 2 * style.ItemSpacing.X;
        widths[0] = Math.Max(widths[0], ComboWidth(new[] { "Proximity", "Emote" }));
        widths[1] = Math.Max(widths[1], ImGui.GetFrameHeight() + 2 * style.ItemSpacing.X);
        widths[2] = Math.Max(widths[2], ImGui.GetFrameHeight() + 2 * style.ItemSpacing.X);
        widths[3] = Math.Max(widths[3], 80 * scale + UiGui.ButtonWidth("K") + 3 * style.ItemSpacing.X);
        widths[4] = Math.Max(widths[4], 80 * scale + 2 * style.ItemSpacing.X);
        foreach (var column in new[] { 5, 6, 7, 9 }) widths[column] = Math.Max(widths[column], field);
        widths[8] = Math.Max(widths[8], ComboWidth(WeatherService.GetWeatherTypes()));
        widths[10] = Math.Max(widths[10], ImGui.GetFrameHeight() + 2 * style.ItemSpacing.X);
        widths[11] = Math.Max(widths[11], 60 * scale + 2 * style.ItemSpacing.X);
        widths[12] = Math.Max(widths[12], UiGui.ButtonWidth("-") + 2 * style.ItemSpacing.X);
        return widths.Select(MathF.Ceiling).ToArray();
    }

    private static void HelpMarker(string desc)
    {
        ImGui.SameLine();
        UiGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
            UiGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private bool DrawIconInputs(string label, ref string value, string fallback)
    {
        var updated = false;
        var glyph = value;
        ImGui.SetNextItemWidth(80);
        if (UiGui.InputText($"{label} Icon##hfh", ref glyph, 8))
        {
            value = SanitizeIconInput(glyph, fallback);
            updated = true;
        }
        ImGui.SameLine();
        UiGui.TextDisabled(UiText.F("Shown when HFH is {0}", UiText.T(label)));

        var code = FormatIconCode(value);
        ImGui.SetNextItemWidth(160);
        if (UiGui.InputText($"{label} Icon Code##hfh", ref code, 64))
        {
            var parsed = ParseIconCode(code, value);
            value = SanitizeIconInput(parsed, fallback);
            updated = true;
        }

        return updated;
    }

    private static string SanitizeIconInput(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        return trimmed.Length > 3 ? trimmed[..3] : trimmed;
    }

    private static string FormatIconCode(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var sb = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("\\u");
            sb.Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string ParseIconCode(string input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;

        var parts = input.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (sb.Length >= 3) break;

            var token = part.Trim();
            if (token.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
                token = token[2..];
            else if (token.StartsWith("u", StringComparison.OrdinalIgnoreCase))
                token = token[1..];
            else if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token[2..];

            if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codepoint))
            {
                sb.Append(char.ConvertFromUtf32(codepoint));
            }
        }

        return sb.Length == 0 ? fallback : sb.ToString();
    }

    private const string IconGuideUrl = "https://na.finalfantasyxiv.com/lodestone/character/22423564/blog/4393835";

    /// <summary>
    /// Color picker based on Honorific (Caraxi)'s DrawColorPicker method with proper state management
    /// </summary>
    private bool DrawColorPicker(string id, ref Vector3? color, bool readOnly = false)
    {
        var modified = false;
        var displayText = color == null ? UiText.T("No Colour") : PulseTitle.ColorToHex(color);
        
        // Show color button with current color preview
        var buttonColor = color ?? new Vector3(1, 1, 1);
        var buttonClicked = ImGui.ColorButton(id, new Vector4(buttonColor, 1), ImGuiColorEditFlags.None, new Vector2(60, 20));
        
        if (buttonClicked)
        {
            if (!readOnly)
            {
                // Initialize editing color when opening popup
                editingColors[id] = color ?? new Vector3(1, 1, 1);
                ImGui.OpenPopup($"##{id}_popup");
            }
            else
            {
                // Read-only - silently ignore
            }
        }
        
        if (ImGui.IsItemHovered())
        {
            UiGui.SetTooltip(readOnly ? "Glow is disabled for this rule." : "Click to change color");
        }
        
        ImGui.SameLine();
        UiGui.Text(displayText);

        // Keep the wrapped popup usable after its opening frame in every locale.
        var scale = MaterialTheme.Metrics.Scale;
        var popupWidth = Math.Max(360 * scale, UiGui.ButtonWidth("Clear") + 240 * scale
            + ImGui.GetStyle().ItemSpacing.X + 2 * ImGui.GetStyle().WindowPadding.X);
        ImGui.SetNextWindowSize(new Vector2(popupWidth, 0));
        if (ImGui.BeginPopup($"##{id}_popup"))
        {
            // Use the maintained editing color
            if (!editingColors.ContainsKey(id))
                editingColors[id] = color ?? new Vector3(1, 1, 1);
            
            var editingColour = editingColors[id];
            
            // Clear button
            if (UiGui.Button("Clear"))
            {
                color = null;
                modified = true;
                ImGui.CloseCurrentPopup();
            }
            
            if (ImGui.IsItemHovered())
            {
                UiGui.SetTooltip("Clear selected colour");
            }
            
            ImGui.SameLine();
            
            // Color picker - this will update the editing color in real-time
            ImGui.SetNextItemWidth(240 * scale);
            if (ImGui.ColorPicker3($"##ColorPick", ref editingColour, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview))
            {
                // Update the stored editing color for real-time preview
                editingColors[id] = editingColour;
            }
            
            // Confirm button
            if (UiGui.Button("Confirm"))
            {
                color = editingColour;
                modified = true;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        return modified;
    }
}
