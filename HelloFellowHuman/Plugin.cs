using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
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
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/hfh";
    
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("HelloFellowHuman");
    
    private ConfigWindow ConfigWindow { get; init; }
    private EmoteEngine EmoteEngine { get; init; }
    private IDtrBarEntry? DtrEntry { get; set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize();
        
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Hello Fellow Human config window. Use '/hfh on|off|enable|disable' to toggle plugin. Use '/hfh preset <id>' to select preset."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        
        EmoteEngine = new EmoteEngine(Framework, ClientState, ObjectTable, CommandManager, Configuration);
        
        SetupDtrBar();
        
        Framework.Update += OnFrameworkUpdate;
    }

    private void SetupDtrBar()
    {
        try
        {
            DtrEntry = DtrBar.Get("HelloFellowHuman");
            UpdateDtrBar();
            DtrEntry.OnClick = (_) =>
            {
                Configuration.Enabled = !Configuration.Enabled;
                SaveConfig();
                UpdateDtrBar();
                Log.Info($"Hello Fellow Human {(Configuration.Enabled ? "enabled" : "disabled")}");
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
        switch (Configuration.DtrBarMode)
        {
            case 1: // icon+text
                var icon1 = Configuration.Enabled ? "\uE03C" : "\uE03D";
                DtrEntry.Text = $"{icon1} HFH";
                break;
            case 2: // icon-only
                var icon2 = Configuration.Enabled ? "\uE03C" : "\uE03D";
                DtrEntry.Text = icon2;
                break;
            default: // text-only
                var status = Configuration.Enabled ? "ON" : "OFF";
                var activePreset = Configuration.GetActivePreset();
                var presetName = activePreset?.Name ?? "None";
                DtrEntry.Text = $"HFH: {status} [{presetName}]";
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        UpdateDtrBar();
    }


    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        EmoteEngine.Dispose();
        
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
            Configuration.Enabled = true;
            SaveConfig();
            UpdateDtrBar();
            Log.Info("Hello Fellow Human enabled");
            return;
        }
        
        if (argLower == "off" || argLower == "disable")
        {
            Configuration.Enabled = false;
            SaveConfig();
            UpdateDtrBar();
            Log.Info("Hello Fellow Human disabled");
            return;
        }
        
        if (argLower.StartsWith("preset "))
        {
            var presetIdStr = argLower.Substring(7).Trim();
            if (int.TryParse(presetIdStr, out var presetId))
            {
                if (presetId >= 0 && presetId < Configuration.Presets.Count)
                {
                    Configuration.SelectedPresetIndex = presetId;
                    SaveConfig();
                    UpdateDtrBar();
                    Log.Info($"Switched to preset [{presetId}] {Configuration.Presets[presetId].Name}");
                }
                else
                {
                    Log.Error($"Invalid preset ID: {presetId}. Valid range: 0-{Configuration.Presets.Count - 1}");
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
