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

/// <summary>
/// Represents an active pulse animation for a player
/// </summary>
public class PulseAnimation
{
    public string PlayerName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public float WaitDuration { get; set; }
    public string Command { get; set; } = string.Empty; // The command to display
    public string ReceivedEmote { get; set; } = string.Empty; // The received emote for COPYCAT
    public bool IsActive => DateTime.UtcNow < StartTime.AddSeconds(WaitDuration + 2); // 2s extra buffer
}

public class EmoteEngine : IDisposable
{
    private readonly Plugin plugin;
    private readonly EmoteDetectionService? emoteDetection;
    
    private DateTime lastCheckTime = DateTime.MinValue;
    private DateTime currentWaitUntil = DateTime.MinValue;
    private DateTime lastPresetLog = DateTime.MinValue;
    private bool hasLoggedWaitStart = false;
    private const float CheckInterval = 1.0f;
    
    // Queue of pending emote responses: (instigatorName, emoteId, receivedCommand)
    private readonly Queue<(string Name, ushort EmoteId, string ReceivedCommand)> pendingEmoteResponses = new();
    
    // Active pulse animations by player name
    private readonly Dictionary<string, PulseAnimation> activePulses = new();
    
    // Last cleanup time for expired animations
    private DateTime lastCleanupTime = DateTime.MinValue;
    private const float CleanupInterval = 5.0f; // Cleanup every 5 seconds
    
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
        
        // Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines for emote: {cmdForEmote}");
        
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
            
            // Check weather filter
            var currentWeather = plugin.WeatherService.GetCurrentWeather();
            if (!plugin.WeatherService.IsWeatherMatch(line.WeatherFilter, currentWeather))
            {
                Plugin.Log.Debug($"[HFH] Weather filter failed: required '{line.WeatherFilter}', current '{currentWeather}'");
                continue;
            }
            Plugin.Log.Debug($"[HFH] Weather filter passed: required '{line.WeatherFilter}', current '{currentWeather}'");
            
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
    
    private void OnFrameworkUpdate(IFramework framework)
    {
        // Cleanup expired animations periodically
        CleanupExpiredAnimations();
        
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null || !account.Enabled) return;
        
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;
        
        var now = DateTime.UtcNow;
        
        // Global WAIT blocking - if any line is in wait mode, block everything
        if (now < currentWaitUntil)
        {
            // Only log wait start once
            if (!hasLoggedWaitStart)
            {
                var remainingWait = (currentWaitUntil - now).TotalSeconds;
                Plugin.Log.Debug($"[HFH] Wait started: waiting until {currentWaitUntil:HH:mm:ss} ({remainingWait:F1}s)");
                hasLoggedWaitStart = true;
            }
            return;
        }
        
        // Log when wait period ends
        if (currentWaitUntil != DateTime.MinValue && now >= currentWaitUntil)
        {
            Plugin.Log.Debug($"[HFH] Wait period ended at {now:HH:mm:ss}, resuming normal operation");
            currentWaitUntil = DateTime.MinValue; // Reset wait state
            hasLoggedWaitStart = false; // Reset wait start flag
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
                // Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines for emote match: {emReceivedCmd}");
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
                    
                    // Fallback logic for COPYCAT if emote copying fails
                    if (triggerCmd == "COPYCAT" && string.IsNullOrEmpty(emReceivedCmd))
                    {
                        commandToExecute = line.SlashCommand; // Use fallback
                        Plugin.Log.Info($"[HFH] COPYCAT fallback: using fallback command '{commandToExecute}'");
                    }
                    
                    Plugin.Log.Info($"[HFH] Emote response: {emInstigator} did {emReceivedCmd} -> executing {commandToExecute}");
                    Plugin.Log.Debug($"[HFH] Wait time tracking: starting wait for {line.WaitTimeAfter}s at {now:HH:mm:ss}");
                    
                    ExecuteLine(line, commandToExecute);
                    
                    // Start pulse animation if enabled
                    if (line.PulseTarget)
                    {
                        StartPulseAnimation(emInstigator, line, commandToExecute, emReceivedCmd);
                    }
                    
                    line.LastExecuted = now;
                    currentWaitUntil = now.AddSeconds(line.WaitTimeAfter);
                    
                    Plugin.Log.Debug($"[HFH] Wait time tracking: global wait set until {currentWaitUntil:HH:mm:ss}");
                    return;
                }
            }
        }
        
        // --- Process proximity-type lines ---
        if (activePreset == null) return;
        var validLines = new List<EmoteLine>();
        var playerPos = localPlayer.Position;
        
        // Plugin.Log.Debug($"[HFH] Checking {activePreset.Lines.Count} lines, player pos: {playerPos}");
        
        foreach (var line in activePreset.Lines)
        {
            // Skip emote-type lines in proximity scan
            if (line.TriggerType == 1) continue;
            
            if (!line.IsValid())
            {
                Plugin.Log.Debug($"[HFH] Line invalid: {line.TargetName}");
                continue;
            }
            
            // Check weather filter
            var currentWeather = plugin.WeatherService.GetCurrentWeather();
            if (!plugin.WeatherService.IsWeatherMatch(line.WeatherFilter, currentWeather))
            {
                Plugin.Log.Debug($"[HFH] Weather filter failed for proximity: required '{line.WeatherFilter}', current '{currentWeather}'");
                continue;
            }
            Plugin.Log.Debug($"[HFH] Weather filter passed for proximity: required '{line.WeatherFilter}', current '{currentWeather}'");
            
            // Check per-line repeat cooldown (skip if RepeatInterval = 0)
            if (line.RepeatInterval > 0)
            {
                if (line.LastExecuted != DateTime.MinValue)
                {
                    // Reset LastExecuted if it's in the future (bug fix)
                    if (line.LastExecuted > now)
                    {
                        Plugin.Log.Debug($"[HFH] LastExecuted is in future (proximity), resetting it");
                        line.LastExecuted = DateTime.MinValue;
                    }
                    else
                    {
                        var timeSinceLastExec = (now - line.LastExecuted).TotalSeconds;
                        Plugin.Log.Debug($"[HFH] Proximity cooldown check: {line.TargetName}, {timeSinceLastExec:F1}s / {line.RepeatInterval}s");
                        if (timeSinceLastExec < line.RepeatInterval)
                        {
                            Plugin.Log.Debug($"[HFH] Proximity repeat cooldown active: {timeSinceLastExec:F1}s < {line.RepeatInterval}s");
                            continue;
                        }
                    }
                }
                else
                {
                    Plugin.Log.Debug($"[HFH] Proximity line never executed before, allowing trigger");
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
            // Plugin.Log.Debug($"[HFH] No valid lines this cycle");
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
    
    /// <summary>
    /// Clean up expired pulse animations to prevent memory leaks
    /// </summary>
    private void CleanupExpiredAnimations()
    {
        var now = DateTime.UtcNow;
        if ((now - lastCleanupTime).TotalSeconds < CleanupInterval)
            return;
            
        lastCleanupTime = now;
        
        var expiredKeys = new List<string>();
        
        foreach (var kvp in activePulses)
        {
            if (!kvp.Value.IsActive)
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in expiredKeys)
        {
            activePulses.Remove(key);
            Plugin.Log.Debug($"[HFH] Cleaned up expired pulse animation for: {key}");
        }
        
        if (expiredKeys.Count > 0)
        {
            Plugin.Log.Debug($"[HFH] Cleanup complete: removed {expiredKeys.Count} expired animations, {activePulses.Count} active remaining");
        }
    }
    
    private void StartPulseAnimation(string playerName, EmoteLine line, string command, string receivedEmote)
    {
        try
        {
            Plugin.Log.Info($"[HFH] Starting pulse animation for {playerName} ({line.PulseStyle}, {line.WaitTimeAfter}s)");
            
            // Create or update pulse animation
            activePulses[playerName] = new PulseAnimation
            {
                PlayerName = playerName,
                StartTime = DateTime.UtcNow,
                WaitDuration = line.WaitTimeAfter,
                Command = command,
                ReceivedEmote = receivedEmote
            };
            
            // Create the actual pulse title based on line configuration
            var pulseTitle = CreatePulseTitleFromLine(line);
            if (pulseTitle != null)
            {
                Plugin.Log.Debug($"[HFH] Created pulse title: {pulseTitle.Emoji} with color {pulseTitle.Color} and glow {pulseTitle.Glow}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[HFH] Pulse animation error for {playerName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Creates a PulseTitle from an EmoteLine configuration
    /// </summary>
    private PulseTitle? CreatePulseTitleFromLine(EmoteLine line)
    {
        try
        {
            var pulseTitle = new PulseTitle
            {
                Style = line.PulseStyle,
                IsPrefix = true
            };
            
            // Set emoji based on trigger - show command or emote
            if (line.TriggerType == 1 && line.TriggerEmote.ToUpperInvariant() == "COPYCAT")
            {
                // For COPYCAT, show the received emote (will be overridden in GetPulseTitleForPlayer)
                pulseTitle.Emoji = line.SlashCommand; // This will be replaced with actual emote
            }
            else
            {
                // Show the slash command for normal triggers
                pulseTitle.Emoji = line.SlashCommand;
            }
            
            // Set color based on style
            switch (line.PulseStyle)
            {
                case "emoji":
                    pulseTitle.Color = line.PulseColor ?? new Vector3(1.0f, 0.0f, 0.0f); // Use configured color or default red
                    pulseTitle.Glow = line.PulseGlow ?? new Vector3(1.0f, 0.5f, 0.5f); // Use configured glow or default pink glow
                    break;
                case "color":
                    pulseTitle.Color = line.PulseColor ?? new Vector3(1.0f, 0.0f, 0.0f); // Use configured color or default red
                    pulseTitle.Glow = Vector3.Zero; // No glow for color-only
                    break;
                case "both":
                    pulseTitle.Color = line.PulseColor ?? new Vector3(1.0f, 0.0f, 0.0f);
                    pulseTitle.Glow = new Vector3(1.0f, 1.0f, 1.0f); // White glow
                    break;
                default:
                    pulseTitle.Color = line.PulseColor ?? new Vector3(1.0f, 0.0f, 0.0f);
                    pulseTitle.Glow = new Vector3(1.0f, 0.5f, 0.5f);
                    break;
            }
            
            return pulseTitle;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[HFH] Error creating pulse title from line: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets the current pulse title for a specific player
    /// </summary>
    /// <param name="playerName">The player name to check</param>
    /// <returns>PulseTitle if active, null otherwise</returns>
    public PulseTitle? GetPulseTitleForPlayer(string playerName)
    {
        if (activePulses.TryGetValue(playerName, out var pulse))
        {
            // Find the emote line that created this pulse to get its configuration
            var account = plugin.ConfigManager.GetCurrentAccount();
            if (account != null)
            {
                var activePreset = account.GetActivePreset();
                if (activePreset != null)
                {
                    foreach (var line in activePreset.Lines)
                    {
                        if (line.PulseTarget && line.TriggerType == 1) // Emote lines with pulse enabled
                        {
                            // Create pulse title based on the line configuration
                            var pulseTitle = CreatePulseTitleFromLine(line);
                            if (pulseTitle != null)
                            {
                                // Set emoji to show command or received emote
                                if (line.TriggerEmote.ToUpperInvariant() == "COPYCAT")
                                {
                                    pulseTitle.Emoji = pulse.ReceivedEmote; // Show the received emote
                                }
                                else
                                {
                                    pulseTitle.Emoji = pulse.Command; // Show the command
                                }
                                return pulseTitle;
                            }
                        }
                    }
                }
            }
            
            // Fallback to basic pulse title if no line found
            var displayText = string.IsNullOrEmpty(pulse.ReceivedEmote) ? pulse.Command : pulse.ReceivedEmote;
            return new PulseTitle
            {
                Emoji = displayText,
                Color = new Vector3(1.0f, 0.0f, 0.0f), // Default red
                Glow = new Vector3(1.0f, 0.5f, 0.5f),  // Default pink glow
                Style = "emoji"
            };
        }
        
        return null;
    }

    /// <summary>
    /// Gets the emoji for a specific animation phase
    /// </summary>
    /// <param name="phase">Current animation phase (0-3)</param>
    /// <returns>Emoji character for the phase</returns>
    private string GetEmojiForPhase(int phase)
    {
        return phase switch
        {
            0 => "💗",   // Heart
            1 => "✨",   // Sparkles
            2 => "💖",   // Sparkling Heart
            3 => "⭐",   // Star
            _ => "💗"
        };
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        if (emoteDetection != null)
            emoteDetection.OnEmoteReceived -= OnEmoteReceived;
    }
}
