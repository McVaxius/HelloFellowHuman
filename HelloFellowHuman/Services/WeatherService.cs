using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace HelloFellowHuman.Services;

public class WeatherService
{
    private readonly IDataManager dataManager;
    private sealed record Observation(ulong Character, uint Territory, string Weather, long SampledAt);
    private Observation observation = new(0, 0, "Unknown", 0);
    private ulong sampledCharacter;
    private uint sampledTerritory;
    private byte candidateWeather;
    private long candidateSince;

    private static readonly List<string> WeatherTypes = new()
    {
        "ALL",
        "Clear Skies",
        "Fair Skies", 
        "Clouds",
        "Fog",
        "Wind",
        "Gales",
        "Rain",
        "Showers",
        "Thunder",
        "Thunderstorms",
        "Dust Storms",
        "Sandstorms",
        "Hot Spells",
        "Heat Waves",
        "Snow",
        "Blizzards",
        "Gloom",
        "Auroras",
        "Darkness",
        "Tension",
        "Storm Clouds",
        "Rough Seas",
        "Louring"
    };

    // Weather name mapping from game data
    private static readonly Dictionary<uint, string> WeatherNames = new()
    {
        { 1, "Clear Skies" },
        { 2, "Fair Skies" },
        { 3, "Clouds" },
        { 4, "Fog" },
        { 5, "Wind" },
        { 6, "Gales" },
        { 7, "Rain" },
        { 8, "Showers" },
        { 9, "Thunder" },
        { 10, "Thunderstorms" },
        { 11, "Dust Storms" },
        { 12, "Sandstorms" },
        { 13, "Hot Spells" },
        { 14, "Heat Waves" },
        { 15, "Snow" },
        { 16, "Blizzards" },
        { 17, "Gloom" },
        { 18, "Auroras" },
        { 19, "Darkness" },
        { 20, "Tension" },
        { 22, "Storm Clouds" },
        { 23, "Rough Seas" },
        { 25, "Louring" },
        { 26, "Heat Waves" },
        { 27, "Gloom" },
        { 28, "Gales" }
    };

    public WeatherService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public static List<string> GetWeatherTypes()
    {
        return WeatherTypes.OrderBy(x => x == "ALL" ? "" : x).ToList();
    }

    internal unsafe void SampleCurrentWeather()
    {
        try
        {
            var ready = ContextReady;
            var env = ready ? EnvManager.Instance() : null;
            Observe(ready ? Plugin.PlayerState.ContentId : 0, ready ? Plugin.ClientState.TerritoryType : 0,
                ready, env == null ? (byte)0 : env->ActiveWeather, System.Environment.TickCount64);
        }
        catch (System.Exception ex)
        {
            Observe(0, 0, false, 0, System.Environment.TickCount64);
            Plugin.Log.Error($"[HFH] Error observing current weather: {ex.Message}");
        }
    }

    private static bool ContextReady => Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer != null
        && Plugin.PlayerState.ContentId != 0 && Plugin.ClientState.TerritoryType != 0
        && !Plugin.Condition[ConditionFlag.BetweenAreas] && !Plugin.Condition[ConditionFlag.BetweenAreas51];

    // Framework-only observation; readers never touch the native weather singleton.
    internal void Observe(ulong character, uint territory, bool ready, byte weather, long now)
    {
        if (!ready || character == 0 || territory == 0 || !WeatherNames.TryGetValue(weather, out var name))
        {
            sampledCharacter = 0; sampledTerritory = 0; candidateWeather = 0;
            Volatile.Write(ref observation, new(0, 0, "Unknown", now));
            return;
        }
        if (sampledCharacter != character || sampledTerritory != territory || candidateWeather != weather || now < candidateSince)
        {
            sampledCharacter = character; sampledTerritory = territory; candidateWeather = weather; candidateSince = now;
            Volatile.Write(ref observation, new(character, territory, "Unknown", now));
            return;
        }
        // Qualify a stable sample through the existing framework callback; no extra timer or callback.
        Volatile.Write(ref observation, new(character, territory, now - candidateSince >= 1000 ? name : "Unknown", now));
    }

    internal string ObservedWeather(ulong character, uint territory, bool ready, long now)
    {
        var current = Volatile.Read(ref observation);
        return ready && current.Character == character && current.Territory == territory
            && now >= current.SampledAt && now - current.SampledAt <= 2000 ? current.Weather : "Unknown";
    }

    public string GetCurrentWeather() => ObservedWeather(Plugin.PlayerState.ContentId,
        Plugin.ClientState.TerritoryType, ContextReady, System.Environment.TickCount64);

    public bool IsWeatherMatch(string requiredWeather, string currentWeather)
    {
        if (requiredWeather == "ALL") return true;
        return requiredWeather.Equals(currentWeather, System.StringComparison.OrdinalIgnoreCase);
    }
}
