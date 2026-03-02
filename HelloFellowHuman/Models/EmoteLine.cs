using System;
using System.Text.Json.Serialization;

namespace HelloFellowHuman.Models;

public class EmoteLine
{
    public string TargetName { get; set; } = string.Empty;
    public string SlashCommand { get; set; } = string.Empty;
    public float WaitTimeAfter { get; set; } = 3.0f;
    public float RepeatInterval { get; set; } = 5.0f;
    public float DistanceThreshold { get; set; } = 5.0f;
    
    [JsonIgnore]
    public DateTime LastExecuted { get; set; } = DateTime.MinValue;
    
    [JsonIgnore]
    public string? ResolvedTargetName { get; set; }
    
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(TargetName) &&
               !string.IsNullOrWhiteSpace(SlashCommand) &&
               WaitTimeAfter >= 0 &&
               RepeatInterval > 0 &&
               DistanceThreshold > 0;
    }
    
    public EmoteLine Clone()
    {
        return new EmoteLine
        {
            TargetName = TargetName,
            SlashCommand = SlashCommand,
            WaitTimeAfter = WaitTimeAfter,
            RepeatInterval = RepeatInterval,
            DistanceThreshold = DistanceThreshold,
            LastExecuted = LastExecuted
        };
    }
    
    public void ResetRuntimeState()
    {
        LastExecuted = DateTime.MinValue;
    }
}
