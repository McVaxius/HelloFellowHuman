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
    private readonly EmoteDetectionService? emoteDetection;
    
    private DateTime lastCheckTime = DateTime.MinValue;
    private DateTime currentWaitUntil = DateTime.MinValue;
    private const float CheckInterval = 1.0f;
    
    // Queue of pending emote-triggered responses: (instigatorName, emoteId)
    private readonly Queue<(string Name, ushort EmoteId)> pendingEmoteResponses = new();
    
    public EmoteEngine(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ICommandManager commandManager,
        Configuration config,
        EmoteDetectionService? emoteDetection = null)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.commandManager = commandManager;
        this.config = config;
        this.emoteDetection = emoteDetection;
        
        if (emoteDetection != null)
            emoteDetection.OnEmoteReceived += OnEmoteReceived;
        
        this.framework.Update += OnFrameworkUpdate;
    }
    
    private void OnEmoteReceived(string instigatorName, ushort emoteId)
    {
        Plugin.Log.Debug($"[HFH] OnEmoteReceived: {instigatorName} -> ID {emoteId}");
        
        var now = DateTime.Now;
        if (!config.Enabled) 
        {
            Plugin.Log.Debug("[HFH] Plugin disabled, ignoring emote");
            return;
        }
        
        var cmdForEmote = emoteDetection?.GetCommandForEmoteId(emoteId);
        if (cmdForEmote == null) 
        {
            Plugin.Log.Debug($"[HFH] No command found for emote ID {emoteId}");
            return;
        }
        
        var activePreset = config.GetActivePreset();
        if (activePreset == null) 
        {
            Plugin.Log.Debug("[HFH] No active preset, ignoring emote");
            return;
        }
        
        Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines for emote: {cmdForEmote}");
        
        // Check if any emote-type lines match this emote
        foreach (var line in activePreset.Lines)
        {
            if (line.TriggerType != 1) continue; // Only emote-type lines
            if (!line.IsValid()) 
            {
                Plugin.Log.Debug($"[HFH] Emote line invalid: trigger='{line.TriggerEmote}', cmd='{line.SlashCommand}'");
                continue;
            }
            
            // Match the trigger emote command
            var triggerCmd = line.TriggerEmote.Trim().ToLowerInvariant();
            var receivedCmd = cmdForEmote.ToLowerInvariant();
            Plugin.Log.Debug($"[HFH] Comparing: trigger='{triggerCmd}' vs received='{receivedCmd}'");
            if (triggerCmd != receivedCmd) continue;
            
            // Check name filtering - "*" or empty means match anyone
            if (!string.IsNullOrWhiteSpace(line.TargetName) && line.TargetName != "*")
            {
                if (!line.TargetName.Equals(instigatorName, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.Debug($"[HFH] Name filter failed: expected '{line.TargetName}', got '{instigatorName}'");
                    continue;
                }
            }
            
            // Check per-line repeat cooldown (skip if RepeatInterval = 0)
            Plugin.Log.Debug($"[HFH] Checking cooldown: RepeatInterval={line.RepeatInterval}, LastExecuted={line.LastExecuted}");
            if (line.RepeatInterval > 0)
            {
                if (line.LastExecuted != DateTime.MinValue)
                {
                    // Reset LastExecuted if it's in the future (bug fix)
                    if (line.LastExecuted > now)
                    {
                        Plugin.Log.Debug($"[HFH] LastExecuted is in future, resetting it");
                        line.LastExecuted = DateTime.MinValue;
                    }
                    else
                    {
                        var timeSinceLast = now - line.LastExecuted;
                        Plugin.Log.Debug($"[HFH] Time since last: {timeSinceLast.TotalSeconds:F1}s");
                        if (timeSinceLast.TotalSeconds < line.RepeatInterval)
                        {
                            Plugin.Log.Debug($"[HFH] Repeat cooldown: {timeSinceLast.TotalSeconds:F1}s < {line.RepeatInterval}s");
                            continue;
                        }
                    }
                }
                else
                {
                    Plugin.Log.Debug($"[HFH] Never executed before, allowing trigger");
                }
            }
            else
            {
                Plugin.Log.Debug($"[HFH] RepeatInterval is 0, no cooldown check");
            }
            
            // Queue the response
            pendingEmoteResponses.Enqueue((instigatorName, emoteId));
            Plugin.Log.Info($"[HFH] Emote queued: {instigatorName} did {cmdForEmote}, will respond with {line.SlashCommand}");
            break;
        }
    }
    
    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!config.Enabled) return;
        
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null) return;
        
        var now = DateTime.UtcNow;
        
        // Global WAIT blocking - if any line is in wait mode, block everything
        if (now < currentWaitUntil)
        {
            Plugin.Log.Debug($"[HFH] In global wait mode until {currentWaitUntil:HH:mm:ss}");
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
        
        // --- Process pending emote responses first ---
        if (pendingEmoteResponses.Count > 0)
        {
            Plugin.Log.Debug($"[HFH] Processing {pendingEmoteResponses.Count} pending emote responses");
            var (emInstigator, emEmoteId) = pendingEmoteResponses.Dequeue();
            var emCmdForEmote = emoteDetection?.GetCommandForEmoteId(emEmoteId);
            Plugin.Log.Debug($"[HFH] Dequeued emote: {emInstigator} -> {emCmdForEmote} (ID {emEmoteId})");
            
            if (activePreset != null && emCmdForEmote != null)
            {
                Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines for emote match: {emCmdForEmote}");
                foreach (var line in activePreset.Lines)
                {
                    if (line.TriggerType != 1) continue;
                    if (!line.IsValid()) continue;
                    
                    var triggerCmd = line.TriggerEmote.Trim().ToLowerInvariant();
                    Plugin.Log.Debug($"[HFH] Checking emote line: trigger='{triggerCmd}' vs received='{emCmdForEmote.ToLowerInvariant()}'");
                    if (triggerCmd != emCmdForEmote.ToLowerInvariant()) continue;
                    
                    // Check name filtering - "*" or empty means match anyone
                    if (!string.IsNullOrWhiteSpace(line.TargetName) && line.TargetName != "*")
                    {
                        if (!line.TargetName.Equals(emInstigator, StringComparison.OrdinalIgnoreCase))
                        {
                            Plugin.Log.Debug($"[HFH] Emote name mismatch: expected '{line.TargetName}', got '{emInstigator}'");
                            continue;
                        }
                    }
                    
                    // Check per-line repeat cooldown (skip if RepeatInterval = 0)
                    if (line.RepeatInterval > 0)
                    {
                        if (line.LastExecuted != DateTime.MinValue)
                        {
                            // Reset LastExecuted if it's in the future (bug fix)
                            if (line.LastExecuted > now)
                            {
                                Plugin.Log.Debug($"[HFH] Emote line LastExecuted is in future, resetting: {line.TriggerEmote}");
                                line.LastExecuted = DateTime.MinValue;
                            }
                            else
                            {
                                var timeSinceLast = now - line.LastExecuted;
                                if (timeSinceLast.TotalSeconds < line.RepeatInterval)
                                {
                                    Plugin.Log.Debug($"[HFH] Emote line on repeat cooldown: {line.TriggerEmote}, {timeSinceLast.TotalSeconds:F1}s / {line.RepeatInterval}s");
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            Plugin.Log.Debug($"[HFH] Emote line never executed before: {line.TriggerEmote}");
                        }
                    }
                    
                    // Execute the response
                    line.ResolvedTargetName = emInstigator;
                    
                    Plugin.Log.Info($"[HFH] Emote response: {emInstigator} did {emCmdForEmote} -> executing {line.SlashCommand}");
                    ExecuteLine(line);
                    line.LastExecuted = now;
                    currentWaitUntil = now.AddSeconds(line.WaitTimeAfter);
                    return;
                }
            }
        }
        
        // --- Process proximity-type lines ---
        if (activePreset == null) return;
        var validLines = new List<EmoteLine>();
        var playerPos = localPlayer.Position;
        
        Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines, player pos: {playerPos}");
        
        foreach (var line in activePreset.Lines)
        {
            // Skip emote-type lines in proximity scan
            if (line.TriggerType == 1) continue;
            
            if (!line.IsValid())
            {
                Plugin.Log.Debug($"[HFH] Line invalid: {line.TargetName}");
                continue;
            }
            
            // Check per-line repeat cooldown
            if (line.LastExecuted != DateTime.MinValue)
            {
                var timeSinceLastExec = (now - line.LastExecuted).TotalSeconds;
                if (timeSinceLastExec < line.RepeatInterval)
                {
                    Plugin.Log.Debug($"[HFH] Line on repeat cooldown: {line.TargetName}, {timeSinceLastExec:F1}s / {line.RepeatInterval}s");
                    continue;
                }
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
        if (emoteDetection != null)
            emoteDetection.OnEmoteReceived -= OnEmoteReceived;
    }
}
