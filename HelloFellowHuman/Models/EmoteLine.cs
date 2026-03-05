using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HelloFellowHuman.Models;

public class EmoteLine
{
    public string TargetName { get; set; } = string.Empty;
    public string SlashCommand { get; set; } = string.Empty;
    public float WaitTimeAfter { get; set; } = 3.0f;
    public float RepeatInterval { get; set; } = 5.0f;
    public float DistanceThreshold { get; set; } = 5.0f;
    public int TriggerType { get; set; } = 0; // 0=Proximity, 1=Emote
    public string TriggerEmote { get; set; } = string.Empty; // e.g. "/wave" - the emote that triggers this response
    
    [JsonIgnore]
    public DateTime LastExecuted { get; set; } = DateTime.MinValue;
    
    [JsonIgnore]
    public string? ResolvedTargetName { get; set; }
    
    [JsonIgnore]
    public HashSet<string> EmoteFiredBy { get; set; } = new(); // tracks who already triggered this emote line (one-time per person)
    
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(SlashCommand)) return false;
        if (WaitTimeAfter < 0) return false;
        
        if (TriggerType == 1) // Emote
        {
            // Emote lines need TriggerEmote, TargetName is optional (can be "*" or empty for ALL), RepeatInterval >= 0
            return !string.IsNullOrWhiteSpace(TriggerEmote) && 
                   TriggerEmote.StartsWith("/") &&
                   RepeatInterval >= 0;
        }
        else // Proximity
        {
            return !string.IsNullOrWhiteSpace(TargetName) &&
                   RepeatInterval > 0 &&
                   DistanceThreshold > 0;
        }
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
            TriggerType = TriggerType,
            TriggerEmote = TriggerEmote,
            LastExecuted = LastExecuted
        };
    }
    
    public void ResetRuntimeState()
    {
        LastExecuted = DateTime.MinValue;
        EmoteFiredBy.Clear();
    }
}
