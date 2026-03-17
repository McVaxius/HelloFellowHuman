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
    public float EmoteRange { get; set; } = 10.0f; // Range for emote triggers (default: 10 yalms)
    public string WeatherFilter { get; set; } = "ALL"; // Weather filter (default: ALL)
    public int TriggerType { get; set; } = 0; // 0=Proximity, 1=Emote
    public string TriggerEmote { get; set; } = string.Empty; // e.g. "/wave" - the emote that triggers this response
    public bool TargetBeforeCommand { get; set; } = true; // If true, /target the player before executing the command
    
    [JsonIgnore]
    public DateTime LastExecuted { get; set; } = DateTime.MinValue;
    
    [JsonIgnore]
    public string? ResolvedTargetName { get; set; }
    
    [JsonIgnore]
    public HashSet<string> EmoteFiredBy { get; set; } = new(); // tracks who already triggered this emote line (one-time per person)
    
    public bool IsValid()
    {
        // COPYCAT doesn't need a SlashCommand since it copies the received emote
        if (TriggerType == 1 && TriggerEmote == "COPYCAT")
        {
            return !string.IsNullOrWhiteSpace(TriggerEmote) && RepeatInterval >= 0;
        }
        
        if (string.IsNullOrWhiteSpace(SlashCommand)) return false;
        if (WaitTimeAfter < 0) return false;
        
        if (TriggerType == 1) // Emote
        {
            // Emote lines need TriggerEmote, TargetName is optional (can be "*" or empty for ALL), RepeatInterval >= 0
            // COPYCAT is a special case that doesn't need to start with "/"
            return !string.IsNullOrWhiteSpace(TriggerEmote) && 
                   (TriggerEmote.StartsWith("/") || TriggerEmote == "COPYCAT") &&
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
            EmoteRange = EmoteRange,
            WeatherFilter = WeatherFilter,
            TriggerType = TriggerType,
            TriggerEmote = TriggerEmote,
            TargetBeforeCommand = TargetBeforeCommand,
            LastExecuted = LastExecuted
        };
    }
    
    public void ResetRuntimeState()
    {
        LastExecuted = DateTime.MinValue;
        EmoteFiredBy.Clear();
    }
}
