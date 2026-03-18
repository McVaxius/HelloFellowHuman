using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using HelloFellowHuman.Models;
using HelloFellowHuman.Windows;
using HelloFellowHuman.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace HelloFellowHuman;

public sealed unsafe class Plugin : IDalamudPlugin
{
    public const string Version = "1.0.0";
    
    // Dynamic version from repo.json
    public static string GetVersion()
    {
        try
        {
            var repoPath = Path.Combine(PluginInterface.ConfigDirectory.FullName, "..", "..", "HelloFellowHuman", "repo.json");
            if (File.Exists(repoPath))
            {
                var json = File.ReadAllText(repoPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var version = doc.RootElement[0].GetProperty("AssemblyVersion").GetString();
                    return version ?? Version;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[HFH] Failed to read version from repo.json: {ex.Message}");
        }
        return Version;
    }
    
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    // Nameplate hook from Caraxi's pattern
    [Signature("40 53 55 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplateDetour))]
    private Hook<UpdateNameplateDelegate>? updateNameplateHook;

    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex);

    private const string CommandName = "/hfh";
    
    public Configuration Configuration { get; init; }
    public ConfigManager ConfigManager { get; init; }
    public readonly WindowSystem WindowSystem = new("HelloFellowHuman");
    
    private ConfigWindow ConfigWindow { get; init; }
    private EmoteEngine EmoteEngine { get; init; }
    public EmoteDetectionService EmoteDetectionService { get; init; }
    public WeatherService WeatherService { get; init; }
    private IDtrBarEntry? DtrEntry { get; set; }
    private bool wasLoggedIn;
    private int loginDetectionDelay;
    
    // Track applied pulse titles to prevent spam and enable cleanup
    private readonly Dictionary<ulong, string> appliedTitles = new();
    
    /// <summary>
    /// Clear all applied pulse titles to restore original nameplates
    /// </summary>
    public void ClearAppliedTitles()
    {
        appliedTitles.Clear();
        Log.Debug("[HFH] Cleared applied titles, nameplates restored to original state");
    }
    
    // Rate limiting to prevent crash
    private DateTime lastHookCall = DateTime.MinValue;
    private const int hookCallInterval = 100; // 100ms minimum between hook calls

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize();
        
        ConfigManager = new ConfigManager(PluginInterface, Log);
        if (!string.IsNullOrEmpty(Configuration.LastAccountId))
            ConfigManager.CurrentAccountId = Configuration.LastAccountId;
        
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Hello Fellow Human config window. Use '/hfh on|off|enable|disable' to toggle plugin. Use '/hfh preset <id>' to select preset."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        
        EmoteDetectionService = new EmoteDetectionService(GameInterop, ObjectTable, DataManager, Log);
        EmoteEngine = new EmoteEngine(this);
        WeatherService = new WeatherService(DataManager);
        
        // Initialize nameplate hook for pulse animation
        GameInterop.InitializeFromAttributes(this);
        Log.Info($"[HFH] Hook initialization: updateNameplateHook is {(updateNameplateHook == null ? "NULL" : "initialized")}");
        updateNameplateHook?.Enable();
        Log.Info($"[HFH] Hook enabled: {(updateNameplateHook?.IsEnabled == true ? "YES" : "NO")}");
        
        SetupDtrBar();
        
        ClientState.Login += OnLoginEvent;
        Framework.Update += OnFrameworkUpdate;
        
        // If already logged in at plugin load, defer detection
        if (ClientState.IsLoggedIn)
        {
            wasLoggedIn = true;
            loginDetectionDelay = 3;
        }
    }

    private void SetupDtrBar()
    {
        try
        {
            DtrEntry = DtrBar.Get("HelloFellowHuman");
            UpdateDtrBar();
            DtrEntry.OnClick = (_) =>
            {
                var account = ConfigManager.GetOrCreateCurrentAccount();
                account.Enabled = !account.Enabled;
                ConfigManager.SaveCurrentAccount();
                UpdateDtrBar();
                Log.Info($"Hello Fellow Human {(account.Enabled ? "enabled" : "disabled")}");
            };
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to setup DTR bar: {ex.Message}");
        }
    }

    private void UpdateDtrBar()
    {
        if (DtrEntry == null) return;
        
        DtrEntry.Shown = Configuration.DtrBarEnabled;
        
        if (!Configuration.DtrBarEnabled) return;
        
        // DTR modes: 0=text-only, 1=icon+text, 2=icon-only
        var iconEnabled = string.IsNullOrEmpty(Configuration.DtrIconEnabled) ? "\uE03C" : Configuration.DtrIconEnabled;
        var iconDisabled = string.IsNullOrEmpty(Configuration.DtrIconDisabled) ? "\uE03D" : Configuration.DtrIconDisabled;
        var account = ConfigManager.GetCurrentAccount();
        var isEnabled = account?.Enabled ?? false;
        var glyph = isEnabled ? iconEnabled : iconDisabled;

        switch (Configuration.DtrBarMode)
        {
            case 1: // icon+text
                DtrEntry.Text = $"{glyph} HFH";
                break;
            case 2: // icon-only
                DtrEntry.Text = glyph;
                break;
            default: // text-only
                var status = isEnabled ? "ON" : "OFF";
                var activePreset = account?.GetActivePreset();
                var presetName = activePreset?.Name ?? "None";
                DtrEntry.Text = $"HFH: {status} [{presetName}]";
                break;
        }
    }

    private void OnLoginEvent()
    {
        loginDetectionDelay = 3;
    }

    private void OnLogin()
    {
        try
        {
            var charName = ObjectTable.LocalPlayer?.Name.ToString() ?? "";
            var worldName = ObjectTable.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "";
            if (!string.IsNullOrEmpty(charName) && !string.IsNullOrEmpty(worldName))
            {
                var contentId = PlayerState.ContentId;
                Log.Info($"[HFH] OnLogin: Character={charName}@{worldName}, ContentId={contentId:X16}");
                ConfigManager.EnsureAccountSelected(contentId, charName);
                ConfigManager.MigrateFromLegacyConfig(Configuration);
                Configuration.LastAccountId = ConfigManager.CurrentAccountId;
                SaveConfig();
                Log.Info($"[HFH] Account selected: {ConfigManager.CurrentAccountId}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[HFH] Error during login detection: {ex.Message}");
        }
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        // Delayed login detection (LocalPlayer may not be ready immediately)
        if (ClientState.IsLoggedIn && !wasLoggedIn)
        {
            wasLoggedIn = true;
            loginDetectionDelay = 3;
        }
        else if (!ClientState.IsLoggedIn && wasLoggedIn)
        {
            wasLoggedIn = false;
            loginDetectionDelay = 0;
        }

        if (loginDetectionDelay > 0)
        {
            loginDetectionDelay--;
            if (loginDetectionDelay == 0)
                OnLogin();
        }

        UpdateDtrBar();
    }


    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLoginEvent;
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        EmoteEngine.Dispose();
        EmoteDetectionService.Dispose();
        
        if (DtrEntry != null)
        {
            DtrEntry.Remove();
        }
        
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var argLower = args.Trim().ToLower();
        
        if (string.IsNullOrEmpty(argLower))
        {
            ToggleConfigUI();
            return;
        }
        
        if (argLower == "on" || argLower == "enable")
        {
            var account = ConfigManager.GetOrCreateCurrentAccount();
            account.Enabled = true;
            ConfigManager.SaveCurrentAccount();
            UpdateDtrBar();
            Log.Info("Hello Fellow Human enabled");
            return;
        }
        
        if (argLower == "off" || argLower == "disable")
        {
            var account = ConfigManager.GetOrCreateCurrentAccount();
            account.Enabled = false;
            ConfigManager.SaveCurrentAccount();
            UpdateDtrBar();
            Log.Info("Hello Fellow Human disabled");
            return;
        }
        
        if (argLower.StartsWith("preset "))
        {
            var account = ConfigManager.GetOrCreateCurrentAccount();
            var presetIdStr = argLower.Substring(7).Trim();
            if (int.TryParse(presetIdStr, out var presetId))
            {
                if (presetId >= 0 && presetId < account.Presets.Count)
                {
                    account.SelectedPresetIndex = presetId;
                    ConfigManager.SaveCurrentAccount();
                    UpdateDtrBar();
                    Log.Info($"Switched to preset [{presetId}] {account.Presets[presetId].Name}");
                }
                else
                {
                    Log.Error($"Invalid preset ID: {presetId}. Valid range: 0-{account.Presets.Count - 1}");
                }
            }
            else
            {
                Log.Error($"Invalid preset ID format: {presetIdStr}");
            }
            return;
        }
        
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    
    public void SaveConfig()
    {
        PluginInterface.SavePluginConfig(Configuration);
        UpdateDtrBar();
    }

    /// <summary>
    /// Nameplate hook detour for pulse animation (based on Caraxi's pattern)
    /// </summary>
    public void* UpdateNameplateDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex)
    {
        try
        {
            // Rate limiting to prevent crash
            var now = DateTime.UtcNow;
            if ((now - lastHookCall).TotalMilliseconds < hookCallInterval)
            {
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            }
            lastHookCall = now;
            
            // Check if HFH is enabled
            var currentAccount = ConfigManager.GetCurrentAccount();
            if (currentAccount == null || !currentAccount.Enabled) 
            {
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            }
            
            // Skip during certain game states to prevent issues
            if (ClientState.IsPvPExcludingDen ||
                Condition[ConditionFlag.BetweenAreas] ||
                Condition[ConditionFlag.BetweenAreas51] ||
                Condition[ConditionFlag.LoggingOut] ||
                Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
                Condition[ConditionFlag.WatchingCutscene] ||
                Condition[ConditionFlag.WatchingCutscene78])
            {
                // Only log game state issues every 10 seconds to avoid spam
                if (DateTime.UtcNow.Second % 10 == 0)
                {
                    Log.Debug("[HFH] Game state prevents hook, returning early");
                }
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            }

            // Only process player nameplates
            if (battleChara == null) 
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            
            var gameObject = &battleChara->Character.GameObject;
            var playerName = battleChara->NameString.ToString();
            
            // Only process ObjectKind.Pc with SubKind=4 (player characters)
            if (gameObject->ObjectKind != FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc || gameObject->SubKind != 4)
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            
            // Check for pulse animation
            var pulseTitle = EmoteEngine.GetPulseTitleForPlayer(playerName);
            if (pulseTitle == null)
            {
                // Only log cleanup when actually removing a title
                if (appliedTitles.TryGetValue(battleChara->EntityId, out var currentTitle))
                {
                    appliedTitles.Remove(battleChara->EntityId);
                    Log.Debug($"[HFH] Restored original title for {playerName}");
                }
                return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
            }
            
            // Log when we actually find a pulse animation
            Log.Debug($"[HFH] Pulse lookup for {playerName}: FOUND {pulseTitle.Emoji}");
            
            // Use SeString encoding for color and glow effects
            var titleSeString = pulseTitle.ToSeString();
            var titleBytes = titleSeString.Encode();
            
            if (titleBytes != null && titleBytes.Length > 0 && namePlateInfo != null)
            {
                // Always apply the title since SeString encoding is working
                try
                {
                    // Set the title as prefix (Caraxi pattern)
                    namePlateInfo->DisplayTitle.SetString(titleBytes);
                    namePlateInfo->IsPrefix = true;
                    namePlateInfo->IsDirty = true;
                    
                    // Only log the first time we apply this specific emoji
                    if (!appliedTitles.TryGetValue(battleChara->EntityId, out var lastTitle) || lastTitle != pulseTitle.Emoji)
                    {
                        appliedTitles[battleChara->EntityId] = pulseTitle.Emoji;
                        Log.Info($"[HFH] Applied pulse title to {playerName}: {pulseTitle.Emoji}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[HFH] Failed to apply pulse title to {playerName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[HFH] Error in nameplate hook: {ex.Message}");
        }
        
        return updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
    }
}
