using System;
using System.Collections.Generic;

namespace HelloFellowHuman.Models;

[Serializable]
public class AccountConfig
{
    public string AccountId { get; set; } = "";
    public string AccountAlias { get; set; } = "";
    public bool Enabled { get; set; } = false;
    public int SelectedPresetIndex { get; set; } = 0;
    public List<EmotePreset> Presets { get; set; } = new();

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
}
