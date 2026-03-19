using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
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
    
    // Pulse Animation Properties (from Caraxi CustomTitle)
    public bool GlowEnabled { get; set; } = false; // Enable glow animation (renamed from PulseTarget)
    
    [JsonConverter(typeof(Vector3Converter))]
    public Vector3? GlowColor { get; set; } = null; // Glow color (RGB 0-1) (renamed from PulseColor)
    
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
            GlowEnabled = GlowEnabled,
            GlowColor = GlowColor,
            LastExecuted = LastExecuted
        };
    }
    
    public void ResetRuntimeState()
    {
        LastExecuted = DateTime.MinValue;
        EmoteFiredBy.Clear();
    }
}

/// <summary>
/// JSON converter for Vector3 to handle nullable Vector3 serialization
/// </summary>
public class Vector3Converter : JsonConverter<Vector3?>
{
    public override Vector3? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read(); // Skip start array
            var x = reader.GetSingle();
            reader.Read(); // Move to next
            var y = reader.GetSingle();
            reader.Read(); // Move to next
            var z = reader.GetSingle();
            reader.Read(); // Skip end array
            return new Vector3(x, y, z);
        }
        
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            reader.Read(); // Skip start object
            var x = 0f;
            var y = 0f;
            var z = 0f;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                    
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read(); // Move to value
                    
                    switch (propertyName)
                    {
                        case "X":
                        case "x":
                            x = reader.GetSingle();
                            break;
                        case "Y":
                        case "y":
                            y = reader.GetSingle();
                            break;
                        case "Z":
                        case "z":
                            z = reader.GetSingle();
                            break;
                    }
                }
            }
            
            return new Vector3(x, y, z);
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Vector3? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Value.X);
        writer.WriteNumberValue(value.Value.Y);
        writer.WriteNumberValue(value.Value.Z);
        writer.WriteEndArray();
    }
}
