using Dalamud.Configuration;
using HelloFellowHuman.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloFellowHuman;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    
    // Legacy fields (kept for migration to per-account config)
    public bool Enabled { get; set; } = true;
    public bool DtrBarEnabled { get; set; } = true;
    public int DtrBarMode { get; set; } = 0; // 0=text-only, 1=icon+text, 2=icon-only
    public string DtrIconEnabled { get; set; } = "\uE03C";
    public string DtrIconDisabled { get; set; } = "\uE03D";
    public bool KrangleEnabled { get; set; } = false;
    public int SelectedPresetIndex { get; set; } = 0;
    public List<EmotePreset> Presets { get; set; } = new();
    
    // Account tracking
    public string LastAccountId { get; set; } = "";
    
    public void Initialize()
    {
        if (Presets.Count == 0)
        {
            Presets.Add(new EmotePreset
            {
                Name = "DEFAULT PRESET",
                Lines = new List<EmoteLine>
                {
                    new EmoteLine
                    {
                        TargetName = "Example Player",
                        SlashCommand = "/wave",
                        WaitTimeAfter = 3.0f,
                        RepeatInterval = 5.0f,
                        DistanceThreshold = 5.0f
                    }
                }
            });
        }
    }
    
    public EmotePreset? GetActivePreset()
    {
        if (SelectedPresetIndex >= 0 && SelectedPresetIndex < Presets.Count)
            return Presets[SelectedPresetIndex];
        return null;
    }
    
    public EmotePreset GetDefaultPreset()
    {
        return Presets.FirstOrDefault(p => p.Name == "DEFAULT PRESET") ?? Presets[0];
    }
    
    public void AddPreset(string name)
    {
        var defaultPreset = GetDefaultPreset();
        var newPreset = defaultPreset.Clone();
        newPreset.Name = name;
        Presets.Add(newPreset);
    }
    
    public void DeletePreset(int index)
    {
        if (index > 0 && index < Presets.Count && Presets[index].Name != "DEFAULT PRESET")
        {
            Presets.RemoveAt(index);
            if (SelectedPresetIndex >= Presets.Count)
                SelectedPresetIndex = Presets.Count - 1;
        }
    }
}
