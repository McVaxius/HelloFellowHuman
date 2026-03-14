using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace HelloFellowHuman.Services;

/// <summary>
/// Detects emotes directed at the local player using a game hook.
/// Pattern lifted from EmoteLog by RokasKil (https://github.com/RokasKil/EmoteLog).
/// </summary>
public class EmoteDetectionService : IDisposable
{
    public delegate void EmoteReceivedDelegate(string instigatorName, ushort emoteId);
    public event EmoteReceivedDelegate? OnEmoteReceived;

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate>? hookEmote;

    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    // Emote ID -> slash command mapping (built from Lumina)
    private readonly Dictionary<ushort, string> emoteIdToCommand = new();
    // Slash command -> emote ID mapping (for config lookup)
    private readonly Dictionary<string, ushort> commandToEmoteId = new();
    // All known emote commands sorted for UI dropdown
    public string[] EmoteCommands { get; private set; } = Array.Empty<string>();

    public EmoteDetectionService(
        IGameInteropProvider gameInterop,
        IObjectTable objectTable,
        IDataManager dataManager,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.log = log;

        BuildEmoteLookup(dataManager);

        try
        {
            hookEmote = gameInterop.HookFromSignature<OnEmoteFuncDelegate>(
                "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24",
                OnEmoteDetour);
            hookEmote.Enable();
            log.Information($"[HFH] Emote detection hook enabled. {emoteIdToCommand.Count} emotes mapped.");
        }
        catch (Exception ex)
        {
            log.Error($"[HFH] Failed to hook emote function: {ex.Message}");
        }
    }

    private void BuildEmoteLookup(IDataManager dataManager)
    {
        try
        {
            var emoteSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Emote>();
            if (emoteSheet == null)
            {
                log.Warning("[HFH] Emote sheet not found");
                return;
            }

            var commands = new List<string>();

            foreach (var emote in emoteSheet)
            {
                if (emote.RowId == 0) continue;

                try
                {
                    var textCmd = emote.TextCommand.Value;
                    var cmdStr = textCmd.Command.ToString();
                    if (string.IsNullOrEmpty(cmdStr)) continue;

                    var id = (ushort)emote.RowId;
                    emoteIdToCommand[id] = cmdStr;

                    if (!commandToEmoteId.ContainsKey(cmdStr))
                    {
                        commandToEmoteId[cmdStr] = id;
                        commands.Add(cmdStr);
                    }

                    // Also map the short alias if it exists
                    var shortCmd = textCmd.ShortCommand.ToString();
                    if (!string.IsNullOrEmpty(shortCmd) && !commandToEmoteId.ContainsKey(shortCmd))
                    {
                        commandToEmoteId[shortCmd] = id;
                    }
                }
                catch
                {
                    // Some emotes may not have text commands
                }
            }

            commands.Sort(StringComparer.OrdinalIgnoreCase);
            
            // Add COPYCAT as a special emote option
            var copycatList = commands.ToList();
            copycatList.Add("COPYCAT");
            copycatList.Sort(StringComparer.OrdinalIgnoreCase);
            EmoteCommands = copycatList.ToArray();
            
            log.Information($"[HFH] Built emote lookup: {emoteIdToCommand.Count} IDs, {EmoteCommands.Length} commands (including COPYCAT)");
        }
        catch (Exception ex)
        {
            log.Error($"[HFH] Failed to build emote lookup: {ex.Message}");
        }
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            var localPlayer = objectTable.LocalPlayer;
            if (localPlayer != null && targetId == localPlayer.GameObjectId)
            {
                var instigator = objectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr);
                if (instigator is IPlayerCharacter pc)
                {
                    var name = pc.Name.TextValue;
                    var cmdName = emoteIdToCommand.TryGetValue(emoteId, out var cmd) ? cmd : $"#{emoteId}";
                    log.Debug($"[HFH] Emote received: {name} -> {cmdName} (ID {emoteId})");
                    OnEmoteReceived?.Invoke(name, emoteId);
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"[HFH] Emote detour error: {ex.Message}");
        }

        hookEmote!.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    /// <summary>
    /// Get the slash command for an emote ID, or null if unknown.
    /// </summary>
    public string? GetCommandForEmoteId(ushort emoteId)
    {
        return emoteIdToCommand.TryGetValue(emoteId, out var cmd) ? cmd : null;
    }

    /// <summary>
    /// Get the emote ID for a slash command, or 0 if unknown.
    /// </summary>
    public ushort GetEmoteIdForCommand(string command)
    {
        return commandToEmoteId.TryGetValue(command, out var id) ? id : (ushort)0;
    }

    public void Dispose()
    {
        hookEmote?.Dispose();
    }
}
