using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using HelloFellowHuman.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace HelloFellowHuman.Services;

public class EmoteEngine : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ICommandManager commandManager;
    private readonly Configuration config;
    
    private DateTime lastCheckTime = DateTime.MinValue;
    private DateTime currentWaitUntil = DateTime.MinValue;
    private const float CheckInterval = 1.0f;
    
    public EmoteEngine(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ICommandManager commandManager,
        Configuration config)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.commandManager = commandManager;
        this.config = config;
        
        this.framework.Update += OnFrameworkUpdate;
    }
    
    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!config.Enabled) return;
        
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null) return;
        
        var now = DateTime.UtcNow;
        
        if (now < currentWaitUntil)
        {
            return;
        }
        
        if ((now - lastCheckTime).TotalSeconds < CheckInterval) return;
        lastCheckTime = now;
        
        var activePreset = config.GetActivePreset();
        if (activePreset == null || activePreset.Lines.Count == 0)
        {
            Plugin.Log.Debug("[HFH] No active preset or empty preset");
            return;
        }
        
        var validLines = new List<EmoteLine>();
        var playerPos = localPlayer.Position;
        
        Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines, player pos: {playerPos}");
        
        foreach (var line in activePreset.Lines)
        {
            if (!line.IsValid())
            {
                Plugin.Log.Debug($"[HFH] Line invalid: {line.TargetName}");
                continue;
            }
            
            var timeSinceLastExec = (now - line.LastExecuted).TotalSeconds;
            if (timeSinceLastExec < line.RepeatInterval)
            {
                Plugin.Log.Debug($"[HFH] Line on cooldown: {line.TargetName}, {timeSinceLastExec:F1}s / {line.RepeatInterval}s");
                continue;
            }
            
            // "*" means all nearby players
            if (line.TargetName.Trim() == "*")
            {
                var nearbyPlayer = FindNearestPlayer(playerPos, line.DistanceThreshold);
                if (nearbyPlayer != null)
                {
                    var dist = Vector3.Distance(playerPos, nearbyPlayer.Position);
                    Plugin.Log.Info($"[HFH] Wildcard line VALID: {nearbyPlayer.Name.TextValue} @ {dist:F2}y, cmd: {line.SlashCommand}");
                    // Temporarily set the target name for execution
                    line.ResolvedTargetName = nearbyPlayer.Name.TextValue;
                    validLines.Add(line);
                }
                continue;
            }
            
            var target = FindPlayerByName(line.TargetName);
            if (target == null)
            {
                Plugin.Log.Debug($"[HFH] Target not found: {line.TargetName}");
                continue;
            }
            
            var distance = Vector3.Distance(playerPos, target.Position);
            Plugin.Log.Debug($"[HFH] Target {line.TargetName} found at distance {distance:F2} (threshold: {line.DistanceThreshold})");
            
            if (distance <= line.DistanceThreshold)
            {
                line.ResolvedTargetName = null;
                Plugin.Log.Info($"[HFH] Line VALID: {line.TargetName} @ {distance:F2}y, cmd: {line.SlashCommand}");
                validLines.Add(line);
            }
        }
        
        if (validLines.Count == 0)
        {
            Plugin.Log.Debug($"[HFH] No valid lines this cycle");
            return;
        }
        
        Plugin.Log.Info($"[HFH] {validLines.Count} valid line(s), selecting random");
        
        var random = new Random();
        var shuffled = validLines.OrderBy(_ => random.Next()).ToList();
        
        foreach (var line in shuffled)
        {
            Plugin.Log.Info($"[HFH] Executing: {line.TargetName} -> {line.SlashCommand}");
            ExecuteLine(line);
            line.LastExecuted = now;
            currentWaitUntil = now.AddSeconds(line.WaitTimeAfter);
            Plugin.Log.Info($"[HFH] Wait until {currentWaitUntil:HH:mm:ss} ({line.WaitTimeAfter}s)");
            break;
        }
    }
    
    private IGameObject? FindPlayerByName(string name)
    {
        var cleanName = name.Trim();
        foreach (var obj in objectTable)
        {
            if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
            {
                var pcName = obj.Name.TextValue;
                if (pcName.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }
        }
        return null;
    }
    
    private IGameObject? FindNearestPlayer(Vector3 playerPos, float maxDistance)
    {
        var localPlayer = objectTable.LocalPlayer;
        IGameObject? nearest = null;
        var nearestDist = float.MaxValue;
        
        foreach (var obj in objectTable)
        {
            if (obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player) continue;
            if (obj.GameObjectId == localPlayer?.GameObjectId) continue;
            
            var dist = Vector3.Distance(playerPos, obj.Position);
            if (dist <= maxDistance && dist < nearestDist)
            {
                nearest = obj;
                nearestDist = dist;
            }
        }
        return nearest;
    }
    
    private void ExecuteLine(EmoteLine line)
    {
        try
        {
            Plugin.Log.Info($"[HFH] Targeting: {line.TargetName}");
            var targetName = line.ResolvedTargetName ?? line.TargetName;
            SendChatCommand($"/target {targetName}");
            System.Threading.Thread.Sleep(500);
            Plugin.Log.Info($"[HFH] Sending command: {line.SlashCommand}");
            SendChatCommand(line.SlashCommand);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[HFH] Failed to execute emote line: {ex.Message}");
        }
    }
    
    private unsafe void SendChatCommand(string command)
    {
        try
        {
            // Try plugin command first
            if (commandManager.ProcessCommand(command))
                return;
            
            // Fall back to game command via UIModule
            var uiModule = UIModule.Instance();
            if (uiModule == null)
            {
                Plugin.Log.Error("[HFH] UIModule is null, cannot send command");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(command);
            var utf8String = Utf8String.FromSequence(bytes);
            uiModule->ProcessChatBoxEntry(utf8String, nint.Zero);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[HFH] Failed to send chat command: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
    }
}
