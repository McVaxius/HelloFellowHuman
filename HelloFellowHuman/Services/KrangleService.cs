using System;
using System.Collections.Generic;

namespace HelloFellowHuman.Services;

/// <summary>
/// Obfuscates player names using deterministic exercise-word substitution.
/// Ported from FrenRider's KrangleService.
/// </summary>
public static class KrangleService
{
    private static readonly string[] ExerciseWords =
    {
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot",
        "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima",
        "Mike", "November", "Oscar", "Papa", "Quebec", "Romeo",
        "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray",
        "Yankee", "Zulu", "Anvil", "Bastion", "Cipher", "Dagger",
        "Ember", "Falcon", "Granite", "Harpoon", "Iron", "Javelin",
        "Kraken", "Lynx", "Mantis", "Nimbus", "Onyx", "Phantom",
        "Quartz", "Raptor", "Sentry", "Thunder", "Umbra", "Viper",
        "Warden", "Xenon", "Yonder", "Zenith",
    };

    private static readonly Dictionary<string, string> Cache = new();

    public static string KrangleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        if (Cache.TryGetValue(name, out var cached))
            return cached;

        var parts = name.Split(' ', '@');
        var result = new List<string>();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var hash = GetStableHash(part);
            var word = ExerciseWords[Math.Abs(hash) % ExerciseWords.Length];
            result.Add(word);
        }

        var krangled = string.Join(" ", result);
        Cache[name] = krangled;
        return krangled;
    }

    public static string KrangleServer(string server)
    {
        if (string.IsNullOrWhiteSpace(server)) return server;

        if (Cache.TryGetValue($"srv:{server}", out var cached))
            return cached;

        var hash = GetStableHash(server);
        var word = ExerciseWords[Math.Abs(hash) % ExerciseWords.Length];
        var krangled = $"{word} Server";
        Cache[$"srv:{server}"] = krangled;
        return krangled;
    }

    public static void ClearCache() => Cache.Clear();

    private static int GetStableHash(string str)
    {
        unchecked
        {
            int hash = 17;
            foreach (var c in str)
                hash = hash * 31 + c;
            return hash;
        }
    }
}
