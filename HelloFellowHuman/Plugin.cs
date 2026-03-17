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

namespace HelloFellowHuman;

public sealed class Plugin : IDalamudPlugin
{
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
}
