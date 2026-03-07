using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HelloFellowHuman.Models;

namespace HelloFellowHuman.Services;

public class ConfigManager
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly string configDir;

    private readonly Dictionary<string, AccountConfig> accounts = new();

    public string CurrentAccountId { get; set; } = "";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public ConfigManager(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
        configDir = pluginInterface.GetPluginConfigDirectory();
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        LoadAllAccounts();
    }

    public IReadOnlyDictionary<string, AccountConfig> Accounts => accounts;

    public AccountConfig? GetCurrentAccount()
    {
        if (string.IsNullOrEmpty(CurrentAccountId)) return null;
        return accounts.TryGetValue(CurrentAccountId, out var acc) ? acc : null;
    }

    public AccountConfig GetOrCreateCurrentAccount()
    {
        var account = GetCurrentAccount();
        if (account != null) return account;

        // Fallback: use first account or create one
        if (accounts.Count > 0)
        {
            var first = accounts.First();
            CurrentAccountId = first.Key;
            return first.Value;
        }

        var fallbackId = Guid.NewGuid().ToString("N")[..8];
        var fallback = new AccountConfig
        {
            AccountId = fallbackId,
            AccountAlias = "Default Account",
        };
        fallback.Initialize();
        accounts[fallbackId] = fallback;
        CurrentAccountId = fallbackId;
        SaveAccount(fallbackId);
        log.Warning($"[HFH] Created fallback account {fallbackId}");
        return fallback;
    }

    public void EnsureAccountSelected(ulong contentId, string? aliasHint = null)
    {
        if (contentId == 0)
        {
            log.Warning("[HFH] Cannot select account with content ID 0 - using fallback");
            if (accounts.Count > 0)
            {
                CurrentAccountId = accounts.Keys.First();
                return;
            }

            var fallbackId = Guid.NewGuid().ToString("N")[..8];
            var fallbackAccount = new AccountConfig
            {
                AccountId = fallbackId,
                AccountAlias = aliasHint ?? "Fallback Account",
            };
            fallbackAccount.Initialize();
            accounts[fallbackId] = fallbackAccount;
            CurrentAccountId = fallbackId;
            SaveAccount(fallbackId);
            return;
        }

        var accountId = contentId.ToString("X");
        log.Information($"[HFH] EnsureAccountSelected: ContentId={contentId:X16}, AccountId={accountId}");

        if (!accounts.TryGetValue(accountId, out var account))
        {
            if (accounts.Count == 1)
            {
                // Migration: move single legacy account to new ID
                var kvp = accounts.First();
                var oldId = kvp.Key;
                account = kvp.Value;
                accounts.Remove(oldId);
                account.AccountId = accountId;
                accounts[accountId] = account;

                try
                {
                    var oldFile = Path.Combine(configDir, $"{oldId}_HFH.json");
                    if (File.Exists(oldFile))
                        File.Delete(oldFile);
                }
                catch (Exception ex)
                {
                    log.Warning($"[HFH] Failed to delete legacy config file for {oldId}: {ex.Message}");
                }

                SaveAccount(accountId);
                log.Information($"[HFH] Migrated legacy account {oldId} -> {accountId}");
            }
            else
            {
                account = new AccountConfig
                {
                    AccountId = accountId,
                    AccountAlias = !string.IsNullOrWhiteSpace(aliasHint)
                        ? aliasHint
                        : $"Account {accounts.Count + 1}",
                };
                account.Initialize();
                accounts[accountId] = account;
                SaveAccount(accountId);
                log.Information($"[HFH] Created account {accountId} ({account.AccountAlias})");
            }
        }
        else if (!string.IsNullOrWhiteSpace(aliasHint) && string.IsNullOrWhiteSpace(account.AccountAlias))
        {
            account.AccountAlias = aliasHint;
            SaveAccount(accountId);
        }

        CurrentAccountId = accountId;
    }

    /// <summary>
    /// Migrate presets from legacy Configuration into the current account.
    /// Only runs once (when account has no presets but Configuration does).
    /// </summary>
    public void MigrateFromLegacyConfig(Configuration legacyConfig)
    {
        var account = GetCurrentAccount();
        if (account == null) return;

        // Only migrate if account has default/empty presets and legacy has real data
        if (account.Presets.Count <= 1 && legacyConfig.Presets.Count > 0)
        {
            var hasOnlyDefault = account.Presets.Count == 1 &&
                                 account.Presets[0].Name == "DEFAULT PRESET" &&
                                 account.Presets[0].Lines.Count == 1 &&
                                 account.Presets[0].Lines[0].TargetName == "Example Player";

            if (account.Presets.Count == 0 || hasOnlyDefault)
            {
                account.Presets = legacyConfig.Presets.Select(p => p.Clone()).ToList();
                account.SelectedPresetIndex = legacyConfig.SelectedPresetIndex;
                account.Enabled = legacyConfig.Enabled;
                SaveCurrentAccount();
                log.Information($"[HFH] Migrated {legacyConfig.Presets.Count} presets from legacy config to account {account.AccountId}");
            }
        }
    }

    public void SaveCurrentAccount()
    {
        if (!string.IsNullOrEmpty(CurrentAccountId))
            SaveAccount(CurrentAccountId);
    }

    private void LoadAllAccounts()
    {
        try
        {
            var files = Directory.GetFiles(configDir, "*_HFH.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var account = JsonSerializer.Deserialize<AccountConfig>(json, JsonOptions);
                    if (account != null && !string.IsNullOrEmpty(account.AccountId))
                    {
                        account.Initialize();
                        accounts[account.AccountId] = account;
                        log.Information($"[HFH] Loaded account {account.AccountId} ({account.AccountAlias}) with {account.Presets.Count} presets");
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[HFH] Failed to load config file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"[HFH] Failed to enumerate config files: {ex.Message}");
        }
    }

    private void SaveAccount(string accountId)
    {
        if (!accounts.TryGetValue(accountId, out var account)) return;

        try
        {
            var fileName = $"{accountId}_HFH.json";
            var filePath = Path.Combine(configDir, fileName);
            var json = JsonSerializer.Serialize(account, JsonOptions);
            File.WriteAllText(filePath, json);
            log.Debug($"[HFH] Saved account {accountId}");
        }
        catch (Exception ex)
        {
            log.Error($"[HFH] Failed to save account {accountId}: {ex.Message}");
        }
    }
}
