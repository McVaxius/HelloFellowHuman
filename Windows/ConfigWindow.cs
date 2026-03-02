using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HelloFellowHuman.Models;
using System;
using System.Numerics;

namespace HelloFellowHuman.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly Plugin plugin;
    
    private int selectedPresetIndex = 0;
    private string newPresetName = string.Empty;
    private string importText = string.Empty;
    
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
        var windowSize = ImGui.GetWindowSize();
        var leftPanelWidth = 200f;
        
        ImGui.BeginChild("LeftPanel", new Vector2(leftPanelWidth, -1), true);
        DrawLeftPanel();
        ImGui.EndChild();
        
        ImGui.SameLine();
        
        ImGui.BeginChild("RightPanel", new Vector2(-1, -1), true);
        DrawRightPanel();
        ImGui.EndChild();
    }
    
    private void DrawLeftPanel()
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
            
            var displayName = $"[{i}] {preset.Name}";
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
    
    private void DrawRightPanel()
    {
        if (selectedPresetIndex < 0 || selectedPresetIndex >= config.Presets.Count)
            return;
        
        var preset = config.Presets[selectedPresetIndex];
        
        ImGui.Text($"Editing: {preset.Name}");
        ImGui.Separator();
        
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
        
        ImGui.Columns(6, "EmoteColumns");
        ImGui.Text("Name");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Target player name (without @server)");
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
            ImGui.SetTooltip("Seconds before this emote can trigger again");
        ImGui.NextColumn();
        ImGui.Text("Distance");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Maximum distance (yalms) to trigger");
        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.Separator();
        
        for (int i = 0; i < preset.Lines.Count; i++)
        {
            var line = preset.Lines[i];
            var isValid = line.IsValid();
            
            if (!isValid)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
            
            var name = line.TargetName;
            if (ImGui.InputText($"##name{i}", ref name, 100))
            {
                line.TargetName = name;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            var cmd = line.SlashCommand;
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
            
            var repeat = line.RepeatInterval;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat($"##repeat{i}", ref repeat, 0.1f, 0.1f, 300f))
            {
                line.RepeatInterval = repeat;
                plugin.SaveConfig();
            }
            ImGui.NextColumn();
            
            var dist = line.DistanceThreshold;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat($"##dist{i}", ref dist, 0.1f, 0.1f, 100f))
            {
                line.DistanceThreshold = dist;
                plugin.SaveConfig();
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
}
