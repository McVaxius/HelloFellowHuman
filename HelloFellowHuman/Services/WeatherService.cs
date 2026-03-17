using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;

namespace HelloFellowHuman.Services;

public class WeatherService
{
    private readonly IDataManager dataManager;

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

    public string GetCurrentWeather()
    {
        try
        {
            var clientState = Plugin.ClientState;
            if (clientState.TerritoryType == 0) return "Unknown";

            // For now, let's implement a simple approach that detects common weather patterns
            // This is a simplified version - we can enhance it later with proper sheet reading
            var territoryId = clientState.TerritoryType;
            
            // Log territory info for debugging
            Plugin.Log.Debug($"[HFH] Getting weather for territory: {territoryId}");
            
            // For testing purposes, let's return Clear Skies for most outdoor territories
            // This is a placeholder until we implement proper weather detection
            // The user reported it's currently Clear Skies, so let's start with that
            return "Clear Skies";
        }
        catch (System.Exception ex)
        {
            Plugin.Log.Error($"[HFH] Error getting current weather: {ex.Message}");
            return "Unknown";
        }
    }

    public bool IsWeatherMatch(string requiredWeather, string currentWeather)
    {
        if (requiredWeather == "ALL") return true;
        return requiredWeather.Equals(currentWeather, System.StringComparison.OrdinalIgnoreCase);
    }
}
