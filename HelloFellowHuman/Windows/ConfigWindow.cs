using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HelloFellowHuman.Models;
using HelloFellowHuman.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace HelloFellowHuman.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly Plugin plugin;
    
    private int selectedPresetIndex = 0;
    private string newPresetName = string.Empty;
    private string importText = string.Empty;
    private readonly Dictionary<int, string> emoteSearchFilters = new();
    
    public ConfigWindow(Plugin plugin) : base("Hello Fellow Human Config###HFHConfig")
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;
        
        Size = new Vector2(800, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        selectedPresetIndex = config.SelectedPresetIndex;
    }
    
    public override void Draw()
    {
        if (ImGui.BeginTabBar("HFHTabBar"))
        {
            if (ImGui.BeginTabItem("Configuration"))
            {
                DrawConfigurationTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Presets"))
            {
                DrawPresetsTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    private void DrawConfigurationTab()
    {
        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            config.Enabled = enabled;
            plugin.SaveConfig();
            Plugin.Log.Info($"Hello Fellow Human {(enabled ? "enabled" : "disabled")}");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enable/disable the plugin's emote automation");
        
        ImGui.SameLine();
        
        var dtrEnabled = config.DtrBarEnabled;
        if (ImGui.Checkbox("DTR ON", ref dtrEnabled))
        {
            config.DtrBarEnabled = dtrEnabled;
            plugin.SaveConfig();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Show/hide the DTR bar entry (server info bar)");
        
        ImGui.SameLine();
        
        var dtrMode = config.DtrBarMode;
        var dtrModes = new[] { "Text Only", "Icon+Text", "Icon Only" };
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("DTR Mode", ref dtrMode, dtrModes, dtrModes.Length))
        {
            config.DtrBarMode = dtrMode;
            plugin.SaveConfig();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("DTR bar display mode:\nText Only: 'HFH: ON/OFF [preset]'\nIcon+Text: '⚫ HFH'\nIcon Only: '⚫'");
        
        ImGui.Spacing();
        ImGui.Text("DTR Icons (max 3 characters)");
        ImGui.SameLine();
        HelpMarker("Customize the glyphs shown in icon modes when HFH is enabled/disabled.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy Icon Guide Link##hfh"))
        {
            ImGui.SetClipboardText(IconGuideUrl);
            Plugin.Log.Info("Copied icon guide link to clipboard");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copies the Lodestone blog link with suggested glyphs");

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

        var krangleEnabled = config.KrangleEnabled;
        if (ImGui.Checkbox("Krangle", ref krangleEnabled))
        {
            config.KrangleEnabled = krangleEnabled;
            plugin.SaveConfig();
            KrangleService.ClearCache();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Obfuscate player names with military/exercise words.\nUseful for screenshots.");
    }
    
    private void DrawPresetsTab()
    {
        var leftPanelWidth = 200f;
        
        ImGui.BeginChild("PresetList", new Vector2(leftPanelWidth, -1), true);
        DrawPresetList();
        ImGui.EndChild();
        
        ImGui.SameLine();
        
        ImGui.BeginChild("PresetEditor", new Vector2(-1, -1), true);
        DrawPresetEditor();
        ImGui.EndChild();
    }
    
    private void DrawPresetList()
    {
        ImGui.Text("Presets");
        ImGui.Separator();
        
        if (ImGui.Button("New", new Vector2(-1, 0)))
        {
            ImGui.OpenPopup("NewPresetPopup");
        }
        
        if (ImGui.BeginPopup("NewPresetPopup"))
        {
            ImGui.Text("Enter preset name:");
            ImGui.InputText("##newpreset", ref newPresetName, 100);
            
            if (ImGui.Button("Create"))
            {
                if (!string.IsNullOrWhiteSpace(newPresetName))
                {
                    config.AddPreset(newPresetName);
                    selectedPresetIndex = config.Presets.Count - 1;
                    config.SelectedPresetIndex = selectedPresetIndex;
                    plugin.SaveConfig();
                    newPresetName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                newPresetName = string.Empty;
                ImGui.CloseCurrentPopup();
                ImGui.EndTooltip();
            }
            ImGui.EndPopup();
        }
        
        var ctrlHeld = ImGui.GetIO().KeyCtrl;
        var deleteButtonColor = ctrlHeld ? new Vector4(1, 0, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
        ImGui.PushStyleColor(ImGuiCol.Button, deleteButtonColor);
        
        if (ImGui.Button("Delete", new Vector2(-1, 0)))
        {
            if (ctrlHeld && selectedPresetIndex > 0 && config.Presets[selectedPresetIndex].Name != "DEFAULT PRESET")
            {
                config.DeletePreset(selectedPresetIndex);
                selectedPresetIndex = Math.Min(selectedPresetIndex, config.Presets.Count - 1);
                config.SelectedPresetIndex = selectedPresetIndex;
                plugin.SaveConfig();
            }
        }
        ImGui.PopStyleColor();
        
        if (!ctrlHeld)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Hold CTRL to delete");
        }
        
        ImGui.Separator();
        
        for (int i = 0; i < config.Presets.Count; i++)
        {
            var preset = config.Presets[i];
            var isSelected = i == selectedPresetIndex;
            var isActive = i == config.SelectedPresetIndex;
            
            var presetName = config.KrangleEnabled ? KrangleService.KrangleName(preset.Name) : preset.Name;
            var displayName = $"[{i}] {presetName}";
            if (isActive)
                displayName += " (ACTIVE)";
            
            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 1.0f, 0.2f, 1));
            
            if (ImGui.Selectable(displayName, isSelected))
            {
                selectedPresetIndex = i;
                config.SelectedPresetIndex = i;
                plugin.SaveConfig();
            }
            
            if (isActive)
                ImGui.PopStyleColor();
        }
    }
    
    private void DrawPresetEditor()
    {
        if (selectedPresetIndex < 0 || selectedPresetIndex >= config.Presets.Count)
            return;
        
        var preset = config.Presets[selectedPresetIndex];
        
        var editingName = config.KrangleEnabled ? KrangleService.KrangleName(preset.Name) : preset.Name;
        ImGui.Text($"Editing: {editingName}");
        ImGui.Separator();
        
        if (ImGui.Button("Export"))
        {
            var base64 = preset.ToBase64();
            ImGui.SetClipboardText(base64);
            Plugin.Log.Info("Preset exported to clipboard");
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Import"))
        {
            var clipboardText = ImGui.GetClipboardText();
            var imported = EmotePreset.FromBase64(clipboardText);
            if (imported != null)
            {
                preset.Lines = imported.Lines;
                plugin.SaveConfig();
                Plugin.Log.Info("Preset imported successfully");
            }
            else
            {
                Plugin.Log.Error("Failed to import preset - invalid clipboard data");
            }
        }
        
        if (preset.Name == "DEFAULT PRESET")
        {
            ImGui.SameLine();
            if (ImGui.Button("Reset Default"))
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
                plugin.SaveConfig();
            }
        }
        
        ImGui.Separator();
        ImGui.Text("Emote Lines:");
        ImGui.Separator();
        
        ImGui.Columns(8, "EmoteColumns");
        ImGui.Text("Type");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Proximity = distance-based, Emote = responds to emotes directed at you");
        ImGui.NextColumn();
        ImGui.Text("ALL");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Check to target all nearby players");
        ImGui.NextColumn();
        ImGui.Text("Name");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Target player name (without @server). For Emote type, leave blank to respond to anyone.");
        ImGui.NextColumn();
        ImGui.Text("Command");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Slash command to execute. Try '/wave motion' to emote without text!");
        ImGui.NextColumn();
        ImGui.Text("Wait");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Seconds to wait after executing this emote");
        ImGui.NextColumn();
        ImGui.Text("Repeat");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Seconds before this emote can trigger again (Proximity only)");
        ImGui.NextColumn();
        ImGui.Text("Dist/Emote");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Proximity: max distance (yalms). Emote: the trigger emote slash command.");
        ImGui.NextColumn();
        ImGui.NextColumn();
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
            if (ImGui.Combo($"##type{i}", ref triggerType, "Proximity\0Emote\0"))
            {
                line.TriggerType = triggerType;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            // ALL checkbox column - now editable for all types
            var isEmoteType = line.TriggerType == 1;
            var isAllTargets = line.TargetName == "*";
            if (ImGui.Checkbox($"##all{i}", ref isAllTargets))
            {
                line.TargetName = isAllTargets ? "*" : "";
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            var name = line.TargetName;
            var displayName = config.KrangleEnabled && name != "*" && !string.IsNullOrWhiteSpace(name)
                ? KrangleService.KrangleName(name) : name;
            ImGui.SetNextItemWidth(220);
            
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
                ImGui.InputText($"##name{i}", ref displayName, 100, inputFlags);
            }
            else
            {
                if (ImGui.InputText($"##name{i}", ref displayName, 100, inputFlags))
                {
                    if (!isAllTargets)
                    {
                        line.TargetName = displayName;
                        plugin.SaveConfig();
                    }
                }
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"K##kr{i}"))
            {
                if (name != "*" && !string.IsNullOrWhiteSpace(name))
                {
                    line.TargetName = KrangleService.KrangleName(name);
                    plugin.SaveConfig();
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Krangle this name (permanent obfuscation)");
            ImGui.NextColumn();
            
            var cmd = line.SlashCommand;
            ImGui.SetNextItemWidth(220);
            if (ImGui.InputText($"##cmd{i}", ref cmd, 100))
            {
                line.SlashCommand = cmd;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            var wait = line.WaitTimeAfter;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat($"##wait{i}", ref wait, 0.1f, 0f, 60f))
            {
                line.WaitTimeAfter = wait;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            // Repeat interval is editable for both proximity and emote types
            var repeat = line.RepeatInterval;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat($"##repeat{i}", ref repeat, 0.1f, 0.1f, 300f))
            {
                line.RepeatInterval = repeat;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            if (isEmoteType)
            {
                // Emote type: show emote selector dropdown with search
                var emoteCommands = plugin.EmoteDetectionService.EmoteCommands;
                var triggerEmote = line.TriggerEmote;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo($"##emote{i}", string.IsNullOrEmpty(triggerEmote) ? "(select)" : triggerEmote))
                {
                    if (!emoteSearchFilters.ContainsKey(i))
                        emoteSearchFilters[i] = "";
                    var filter = emoteSearchFilters[i];
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText($"##efilter{i}", ref filter, 64))
                        emoteSearchFilters[i] = filter;
                    
                    var filterLower = filter.ToLowerInvariant();
                    var shown = 0;
                    foreach (var ec in emoteCommands)
                    {
                        if (!string.IsNullOrEmpty(filterLower) && !ec.ToLowerInvariant().Contains(filterLower))
                            continue;
                        
                        var isSelected = ec == triggerEmote;
                        if (ImGui.Selectable(ec, isSelected))
                        {
                            line.TriggerEmote = ec;
                            plugin.SaveConfig();
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                        
                        if (++shown > 50) break; // Limit visible items
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The emote that triggers this response (e.g. /wave)");
            }
            else
            {
                // Proximity type: show distance
                var dist = line.DistanceThreshold;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat($"##dist{i}", ref dist, 0.1f, 0.1f, 100f))
                {
                    line.DistanceThreshold = dist;
                    plugin.SaveConfig();
                }
            }
            ImGui.NextColumn();
            
            if (i > 0 || preset.Lines.Count > 1)
            {
                if (ImGui.Button($"-##del{i}"))
                {
                    preset.Lines.RemoveAt(i);
                    plugin.SaveConfig();
                    break;
                }
            }
            
            ImGui.NextColumn();
            
            if (!isValid)
                ImGui.PopStyleColor();
        }
        
        ImGui.Columns(1);
        ImGui.Separator();
        
        if (ImGui.Button("+##addline", new Vector2(-1, 0)))
        {
            preset.Lines.Add(new EmoteLine
            {
                TargetName = "",
                SlashCommand = "",
                WaitTimeAfter = 3.0f,
                RepeatInterval = 5.0f,
                DistanceThreshold = 5.0f
            });
            plugin.SaveConfig();
        }
    }
    
    public void Dispose()
    {
    }

    private static void HelpMarker(string desc)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private bool DrawIconInputs(string label, ref string value, string fallback)
    {
        var updated = false;
        var glyph = value;
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputText($"{label} Icon##hfh", ref glyph, 8))
        {
            value = SanitizeIconInput(glyph, fallback);
            updated = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled($"Shown when HFH is {label.ToLowerInvariant()}");

        var code = FormatIconCode(value);
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputText($"{label} Icon Code##hfh", ref code, 64))
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
}
