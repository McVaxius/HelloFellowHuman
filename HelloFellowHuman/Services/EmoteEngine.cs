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
    private readonly Plugin plugin;
    private readonly EmoteDetectionService? emoteDetection;
    
    private DateTime lastCheckTime = DateTime.MinValue;
    private DateTime currentWaitUntil = DateTime.MinValue;
    private DateTime lastPresetLog = DateTime.MinValue;
    private const float CheckInterval = 1.0f;
    
    // Queue of pending emote responses: (instigatorName, emoteId, receivedCommand)
    private readonly Queue<(string Name, ushort EmoteId, string ReceivedCommand)> pendingEmoteResponses = new();
    
    public EmoteEngine(Plugin plugin)
    {
        this.plugin = plugin;
        this.emoteDetection = plugin.EmoteDetectionService;
        
        if (emoteDetection != null)
            emoteDetection.OnEmoteReceived += OnEmoteReceived;
        
        Plugin.Framework.Update += OnFrameworkUpdate;
    }
    
    private void OnEmoteReceived(string instigatorName, ushort emoteId)
    {
        Plugin.Log.Debug($"[HFH] OnEmoteReceived: {instigatorName} -> ID {emoteId}");
        
        var now = DateTime.Now;
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null || !account.Enabled) 
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
        
        var activePreset = account.GetActivePreset();
        if (activePreset == null) 
        {
            Plugin.Log.Debug("[HFH] No active preset, ignoring emote");
            return;
        }
        
        // Log preset details when emote received
        Plugin.Log.Info($"[HFH] Emote received - Active preset: {activePreset.Name} with {activePreset.Lines.Count} lines");
        for (int i = 0; i < activePreset.Lines.Count; i++)
        {
            var line = activePreset.Lines[i];
            Plugin.Log.Info($"[HFH] Line {i}: Type={line.TriggerType}, Trigger='{line.TriggerEmote}', Target='{line.TargetName}', Cmd='{line.SlashCommand}'");
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
            var triggerCmd = line.TriggerEmote.Trim().ToUpperInvariant();
            var receivedCmd = cmdForEmote.Trim().ToUpperInvariant();
            
            Plugin.Log.Info($"[HFH] OnEmoteReceived: stored trigger='{line.TriggerEmote}' -> normalized='{triggerCmd}' vs received='{receivedCmd}'");
            
            // Special case: COPYCAT matches any emote
            if (triggerCmd == "COPYCAT")
            {
                Plugin.Log.Info($"[HFH] COPYCAT line matches any emote: {receivedCmd} from {instigatorName}");
                // Continue with COPYCAT logic below
            }
            else if (triggerCmd != receivedCmd)
            {
                Plugin.Log.Debug($"[HFH] Emote mismatch: trigger='{triggerCmd}' vs received='{receivedCmd}'");
                continue;
            }
            
            // Check name filtering - "*" or empty means match anyone
            if (!string.IsNullOrWhiteSpace(line.TargetName) && line.TargetName != "*")
            {
                if (!line.TargetName.Equals(instigatorName, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.Debug($"[HFH] Name filter failed: expected '{line.TargetName}', got '{instigatorName}'");
                    continue;
                }
            }
            
            // Check emote range for TriggerType == 1 (Emote)
            var instigatorPlayer = FindPlayerByName(instigatorName);
            if (instigatorPlayer != null)
            {
                var localPlayer = Plugin.ObjectTable.LocalPlayer;
                if (localPlayer != null)
                {
                    var distance = Vector3.Distance(localPlayer.Position, instigatorPlayer.Position);
                    if (distance > line.EmoteRange)
                    {
                        Plugin.Log.Debug($"[HFH] Emote range check failed: {instigatorName} too far ({distance:F2}y > {line.EmoteRange}y)");
                        continue;
                    }
                    Plugin.Log.Debug($"[HFH] Emote range check passed: {instigatorName} in range ({distance:F2}y <= {line.EmoteRange}y)");
                }
                else
                {
                    Plugin.Log.Debug("[HFH] Local player not found, cannot check emote range");
                    continue;
                }
            }
            else
            {
                Plugin.Log.Debug($"[HFH] Instigator player not found: {instigatorName}");
                continue;
            }
            
            // Check per-line repeat cooldown (skip if RepeatInterval = 0)
            Plugin.Log.Info($"[HFH] Checking cooldown for {line.TriggerEmote}: RepeatInterval={line.RepeatInterval}, LastExecuted={line.LastExecuted}");
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
                        Plugin.Log.Info($"[HFH] Time since last: {timeSinceLast.TotalSeconds:F1}s, cooldown: {line.RepeatInterval}s");
                        if (timeSinceLast.TotalSeconds < line.RepeatInterval)
                        {
                            Plugin.Log.Info($"[HFH] Repeat cooldown active: {timeSinceLast.TotalSeconds:F1}s < {line.RepeatInterval}s");
                            continue;
                        }
                    }
                }
                else
                {
                    Plugin.Log.Info($"[HFH] Never executed before, allowing trigger");
                }
            }
            else
            {
                Plugin.Log.Info($"[HFH] RepeatInterval is 0, no cooldown check");
            }
            
            // Queue the response
            pendingEmoteResponses.Enqueue((instigatorName, emoteId, cmdForEmote));
            Plugin.Log.Info($"[HFH] Emote queued: {instigatorName} did {cmdForEmote}, will respond with {line.SlashCommand}");
            break;
        }
    }
    
    private void OnFrameworkUpdate(IFramework fw)
    {
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null || !account.Enabled) return;
        
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
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
        
        var activePreset = account.GetActivePreset();
        if (activePreset == null || activePreset.Lines.Count == 0)
        {
            Plugin.Log.Debug("[HFH] No active preset or empty preset");
            return;
        }
        
        // Log preset info once per minute
        if (lastPresetLog == DateTime.MinValue || (now - lastPresetLog).TotalMinutes >= 1)
        {
            Plugin.Log.Info($"[HFH] Active preset: {activePreset.Name} with {activePreset.Lines.Count} lines");
            for (int i = 0; i < activePreset.Lines.Count; i++)
            {
                var line = activePreset.Lines[i];
                Plugin.Log.Info($"[HFH] Line {i}: Type={line.TriggerType}, Trigger='{line.TriggerEmote}', Target='{line.TargetName}', Cmd='{line.SlashCommand}'");
            }
            lastPresetLog = now;
        }
        
        // --- Process pending emote responses first ---
        if (pendingEmoteResponses.Count > 0)
        {
            Plugin.Log.Debug($"[HFH] Processing {pendingEmoteResponses.Count} pending emote responses");
            var (emInstigator, emEmoteId, emReceivedCmd) = pendingEmoteResponses.Dequeue();
            Plugin.Log.Debug($"[HFH] Dequeued emote: {emInstigator} -> {emReceivedCmd} (ID {emEmoteId})");
            
            if (activePreset != null && emReceivedCmd != null)
            {
                Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines for emote match: {emReceivedCmd}");
                foreach (var line in activePreset.Lines)
                {
                    if (line.TriggerType != 1) continue;
                    if (!line.IsValid()) continue;
                    
                    var triggerCmd = line.TriggerEmote.Trim().ToUpperInvariant();
                    Plugin.Log.Info($"[HFH] Checking emote line: stored trigger='{line.TriggerEmote}' -> normalized='{triggerCmd}' vs received='{emReceivedCmd.Trim().ToUpperInvariant()}'");
                    
                    // Special case: COPYCAT matches any emote
                    if (triggerCmd != "COPYCAT" && triggerCmd != emReceivedCmd.Trim().ToUpperInvariant())
                    {
                        Plugin.Log.Debug($"[HFH] Emote line mismatch: {triggerCmd} vs {emReceivedCmd}");
                        continue;
                    }
                    
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
                    
                    // For COPYCAT, use the received emote command instead of the configured one
                    var commandToExecute = triggerCmd == "COPYCAT" ? emReceivedCmd : line.SlashCommand;
                    
                    Plugin.Log.Info($"[HFH] Emote response: {emInstigator} did {emReceivedCmd} -> executing {commandToExecute}");
                    ExecuteLine(line, commandToExecute);
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
            ExecuteLine(line, null);
            line.LastExecuted = now;
            currentWaitUntil = now.AddSeconds(line.WaitTimeAfter);
            Plugin.Log.Info($"[HFH] Wait until {currentWaitUntil:HH:mm:ss} ({line.WaitTimeAfter}s)");
            break;
        }
    }
    
    private IGameObject? FindPlayerByName(string name)
    {
        var cleanName = name.Trim();
        foreach (var obj in Plugin.ObjectTable)
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
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        IGameObject? nearest = null;
        var nearestDist = float.MaxValue;
        
        foreach (var obj in Plugin.ObjectTable)
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
    
    private void ExecuteLine(EmoteLine line, string? overrideCommand = null)
    {
        try
        {
            Plugin.Log.Debug($"[HFH] ExecuteLine: {line.TargetName} -> {line.SlashCommand}");
            
            string? targetName = line.ResolvedTargetName ?? line.TargetName;
            
            // Check if we need to target someone first
            if (!string.IsNullOrWhiteSpace(targetName) && targetName != "*")
            {
                if (line.TargetBeforeCommand)
                {
                    Plugin.Log.Info($"[HFH] Targeting: {targetName}");
                    var target = FindPlayerByName(targetName);
                    if (target != null)
                    {
                        // Check distance threshold for proximity-type lines
                        if (line.TriggerType == 0) // Proximity type
                        {
                            var player = Plugin.ObjectTable.LocalPlayer;
                            if (player != null)
                            {
                                var distance = Vector3.Distance(player.Position, target.Position);
                                if (distance > line.DistanceThreshold)
                                {
                                    Plugin.Log.Debug($"[HFH] Target {targetName} too far: {distance:F2}y > {line.DistanceThreshold}y threshold");
                                    return; // Don't execute if too far
                                }
                                Plugin.Log.Debug($"[HFH] Target {targetName} in range: {distance:F2}y <= {line.DistanceThreshold}y threshold");
                            }
                        }
                        
                        Plugin.TargetManager.Target = target;
                    }
                    else
                    {
                        Plugin.Log.Debug($"[HFH] Target not found: {targetName}");
                        return;
                    }
                }
                else
                {
                    Plugin.Log.Info($"[HFH] Skipping target (TargetBeforeCommand=off)");
                }
                
                var commandToExecute = overrideCommand ?? line.SlashCommand;
                Plugin.Log.Info($"[HFH] Sending command: {commandToExecute}");
                SendChatCommand(commandToExecute);
            }
            else
            {
                // No specific target - just execute command
                var commandToExecute = overrideCommand ?? line.SlashCommand;
                Plugin.Log.Info($"[HFH] Sending command: {commandToExecute}");
                SendChatCommand(commandToExecute);
            }
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
            if (Plugin.CommandManager.ProcessCommand(command))
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
        Plugin.Framework.Update -= OnFrameworkUpdate;
        if (emoteDetection != null)
            emoteDetection.OnEmoteReceived -= OnEmoteReceived;
    }
}
