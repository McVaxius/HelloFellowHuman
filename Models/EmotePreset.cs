using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloFellowHuman.Models;

public class EmotePreset
{
    public string Name { get; set; } = string.Empty;
    public List<EmoteLine> Lines { get; set; } = new();
    
    public EmotePreset Clone()
    {
        return new EmotePreset
        {
            Name = Name,
            Lines = Lines.Select(l => l.Clone()).ToList()
        };
    }
    
    public string ToBase64()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }
    
    public static EmotePreset? FromBase64(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return System.Text.Json.JsonSerializer.Deserialize<EmotePreset>(json);
        }
        catch
        {
            return null;
        }
    }
}
