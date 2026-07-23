using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HelloFellowHuman.Models;
using HelloFellowHuman.Services;
using System;
using System.Linq;
using System.Numerics;

namespace HelloFellowHuman.Windows;

internal enum SetupWizardMode
{
    Setup,
    AddRule,
}

internal enum SetupWizardTrigger
{
    Proximity,
    IncomingEmote,
    Copycat,
}

internal sealed class SetupWizardDraft
{
    public int PresetIndex { get; set; }
    public bool EnableAccount { get; set; }
    public SetupWizardTrigger Trigger { get; set; } = SetupWizardTrigger.Proximity;
    public bool SpecificPlayer { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string TriggerEmote { get; set; } = string.Empty;
    public string ResponseCommand { get; set; } = string.Empty;
    public float WaitSeconds { get; set; } = 3.0f;
    public float CooldownSeconds { get; set; } = 5.0f;
    public float ProximityRange { get; set; } = 5.0f;
    public float EmoteRange { get; set; } = 10.0f;
    public string Weather { get; set; } = "ALL";
    public bool TargetBeforeCommand { get; set; } = true;
    public bool GlowEnabled { get; set; }
    public Vector3 GlowColor { get; set; } = new(0.65f, 0.35f, 1.0f);
}

internal sealed class SetupWizardWindow : Window, IDisposable
{
    private const int StageCount = 5;

    private readonly Plugin plugin;
    private SetupWizardMode mode;
    private SetupWizardDraft? draft;
    private int stage;
    private string validationMessage = string.Empty;

    public SetupWizardWindow(Plugin plugin)
        : base("Hello Fellow Human Guided Setup###HFHSetupWizard")
    {
        this.plugin = plugin;
        Size = new Vector2(680, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Open(SetupWizardMode wizardMode, int? requestedPresetIndex = null)
    {
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null || account.Presets.Count == 0)
        {
            Plugin.Log.Warning("[HFH] Guided setup requires a selected account");
            return;
        }

        mode = wizardMode;
        stage = 0;
        validationMessage = string.Empty;

        var presetIndex = requestedPresetIndex ?? account.SelectedPresetIndex;
        if (presetIndex < 0 || presetIndex >= account.Presets.Count)
            presetIndex = 0;

        draft = new SetupWizardDraft
        {
            PresetIndex = presetIndex,
            EnableAccount = account.Enabled,
        };

        IsOpen = true;
    }

    public override void Draw()
    {
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (draft == null || account == null || account.Presets.Count == 0)
        {
            ImGui.TextWrapped("Log in and select an account before using guided setup.");
            if (ImGui.Button("Close"))
                DiscardAndClose();
            return;
        }

        ImGui.Text(mode == SetupWizardMode.Setup ? "Guided Setup" : "Add Rule with Wizard");
        ImGui.SameLine();
        ImGui.TextDisabled($"- Step {stage + 1} of {StageCount}");
        ImGui.ProgressBar((stage + 1) / (float)StageCount, new Vector2(-1, 0), StageTitle(stage));
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        switch (stage)
        {
            case 0:
                DrawDestinationStage(account);
                break;
            case 1:
                DrawTriggerStage();
                break;
            case 2:
                DrawResponseStage();
                break;
            case 3:
                DrawTuningStage();
                break;
            default:
                DrawReviewStage(account);
                break;
        }

        if (!string.IsNullOrEmpty(validationMessage))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 0.35f, 0.35f, 1.0f), validationMessage);
        }

        DrawNavigation();
    }

    private void DrawDestinationStage(AccountConfig account)
    {
        ImGui.TextWrapped("Choose the preset that will receive this rule. Nothing is saved until Finish.");
        ImGui.Spacing();

        var presetNames = new string[account.Presets.Count];
        for (var i = 0; i < account.Presets.Count; i++)
            presetNames[i] = $"[{i}] {account.Presets[i].Name}";

        ImGui.SetNextItemWidth(-1);
        var presetIndex = draft!.PresetIndex;
        if (ImGui.Combo("Destination preset", ref presetIndex, presetNames, presetNames.Length))
            draft.PresetIndex = presetIndex;

        ImGui.Spacing();
        if (mode == SetupWizardMode.Setup)
        {
            var enableAccount = draft.EnableAccount;
            if (ImGui.Checkbox("Enable automatic reactions when I finish", ref enableAccount))
                draft.EnableAccount = enableAccount;
            ImGui.TextDisabled("Clear this if you want to review the new rule in the advanced editor before enabling it.");
        }
        else
        {
            var status = account.Enabled ? "enabled" : "disabled";
            ImGui.TextWrapped($"This account is currently {status}. Add Rule mode will not change that setting.");
        }
    }

    private void DrawTriggerStage()
    {
        ImGui.TextWrapped("Choose what starts the reaction.");
        ImGui.Spacing();

        if (ImGui.RadioButton("A player enters range", draft!.Trigger == SetupWizardTrigger.Proximity))
            draft.Trigger = SetupWizardTrigger.Proximity;
        if (ImGui.RadioButton("A selected emote is performed", draft.Trigger == SetupWizardTrigger.IncomingEmote))
            draft.Trigger = SetupWizardTrigger.IncomingEmote;
        if (ImGui.RadioButton("COPYCAT any incoming emote", draft.Trigger == SetupWizardTrigger.Copycat))
            draft.Trigger = SetupWizardTrigger.Copycat;

        if (draft.Trigger == SetupWizardTrigger.IncomingEmote)
        {
            ImGui.Spacing();
            DrawIncomingEmoteSelector();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Who can trigger it?");
        if (ImGui.RadioButton("Any nearby player", !draft.SpecificPlayer))
            draft.SpecificPlayer = false;
        if (ImGui.RadioButton("One specific player", draft.SpecificPlayer))
            draft.SpecificPlayer = true;

        if (draft.SpecificPlayer)
        {
            var targetName = draft.TargetName;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Player name (without @World)", ref targetName, 100))
                draft.TargetName = targetName;
        }
    }

    private void DrawIncomingEmoteSelector()
    {
        var preview = string.IsNullOrWhiteSpace(draft!.TriggerEmote) ? "Select an emote" : draft.TriggerEmote;
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("Incoming emote", preview))
            return;

        foreach (var emote in plugin.EmoteDetectionService.EmoteCommands)
        {
            if (string.Equals(emote, "COPYCAT", StringComparison.Ordinal))
                continue;

            var selected = string.Equals(emote, draft.TriggerEmote, StringComparison.Ordinal);
            if (ImGui.Selectable(emote, selected))
                draft.TriggerEmote = emote;
            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }

    private void DrawResponseStage()
    {
        if (draft!.Trigger == SetupWizardTrigger.Copycat)
        {
            ImGui.TextWrapped("COPYCAT mirrors the incoming emote. You may add a fallback command for an emote that cannot be mirrored or is already looping.");
            ImGui.Spacing();
            var fallback = draft.ResponseCommand;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Optional fallback command", ref fallback, 200))
                draft.ResponseCommand = fallback;
            ImGui.TextDisabled("Leave this blank for no fallback.");
        }
        else
        {
            ImGui.TextWrapped("Enter the command to run when this rule triggers.");
            ImGui.Spacing();
            var response = draft.ResponseCommand;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Response command", ref response, 200))
                draft.ResponseCommand = response;
            ImGui.TextDisabled("Example: /wave motion");
        }
    }

    private void DrawTuningStage()
    {
        ImGui.TextWrapped("These defaults are ready to use. Adjust only what you need.");
        ImGui.Spacing();

        var wait = draft!.WaitSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.DragFloat("Wait after response (seconds)", ref wait, 0.1f, 0.0f, 60.0f, "%.1f"))
            draft.WaitSeconds = wait;

        var cooldown = draft.CooldownSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.DragFloat("Cooldown (seconds)", ref cooldown, 0.1f, 0.1f, 300.0f, "%.1f"))
            draft.CooldownSeconds = cooldown;

        if (draft.Trigger == SetupWizardTrigger.Proximity)
        {
            var distance = draft.ProximityRange;
            ImGui.SetNextItemWidth(180);
            if (ImGui.DragFloat("Proximity range (yalms)", ref distance, 0.1f, 0.1f, 100.0f, "%.1f"))
                draft.ProximityRange = distance;
        }
        else
        {
            var emoteRange = draft.EmoteRange;
            ImGui.SetNextItemWidth(180);
            if (ImGui.DragFloat("Incoming-emote range (yalms)", ref emoteRange, 0.1f, 0.1f, 100.0f, "%.1f"))
                draft.EmoteRange = emoteRange;
        }

        var weatherTypes = WeatherService.GetWeatherTypes();
        var weatherIndex = weatherTypes.IndexOf(draft.Weather);
        if (weatherIndex < 0)
            weatherIndex = 0;
        ImGui.SetNextItemWidth(260);
        if (ImGui.Combo("Required weather", ref weatherIndex, weatherTypes.ToArray(), weatherTypes.Count))
            draft.Weather = weatherTypes[weatherIndex];

        var targetBeforeCommand = draft.TargetBeforeCommand;
        if (ImGui.Checkbox("Target the triggering player before the command", ref targetBeforeCommand))
            draft.TargetBeforeCommand = targetBeforeCommand;

        var glowEnabled = draft.GlowEnabled;
        if (ImGui.Checkbox("Show a temporary nameplate glow", ref glowEnabled))
            draft.GlowEnabled = glowEnabled;

        if (draft.GlowEnabled)
        {
            var glowColor = draft.GlowColor;
            if (ImGui.ColorEdit3("Glow color", ref glowColor))
                draft.GlowColor = glowColor;
        }
    }

    private void DrawReviewStage(AccountConfig account)
    {
        var presetName = draft!.PresetIndex >= 0 && draft.PresetIndex < account.Presets.Count
            ? account.Presets[draft.PresetIndex].Name
            : "Unavailable";

        ImGui.TextWrapped("Review the rule below. Finish applies it and saves once; Cancel or closing this window leaves configuration unchanged.");
        ImGui.Spacing();
        DrawReviewRow("Preset", presetName);
        if (mode == SetupWizardMode.Setup)
            DrawReviewRow("Account after finish", draft.EnableAccount ? "Enabled" : "Disabled");
        DrawReviewRow("Trigger", TriggerDescription());
        DrawReviewRow("Audience", draft.SpecificPlayer ? draft.TargetName.Trim() : "Any nearby player");
        if (draft.Trigger == SetupWizardTrigger.IncomingEmote)
            DrawReviewRow("Incoming emote", draft.TriggerEmote);
        DrawReviewRow(
            draft.Trigger == SetupWizardTrigger.Copycat ? "Fallback" : "Response",
            string.IsNullOrWhiteSpace(draft.ResponseCommand) ? "None" : draft.ResponseCommand.Trim());
        DrawReviewRow("Timing", $"Wait {draft.WaitSeconds:0.0}s; cooldown {draft.CooldownSeconds:0.0}s");
        DrawReviewRow(
            "Range",
            draft.Trigger == SetupWizardTrigger.Proximity
                ? $"{draft.ProximityRange:0.0} yalms"
                : $"{draft.EmoteRange:0.0} yalms");
        DrawReviewRow("Weather", draft.Weather == "ALL" ? "Any weather" : draft.Weather);
        DrawReviewRow("Target first", draft.TargetBeforeCommand ? "Yes" : "No");
        DrawReviewRow("Nameplate glow", draft.GlowEnabled ? "On" : "Off");
    }

    private void DrawNavigation()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Cancel"))
        {
            DiscardAndClose();
            return;
        }

        if (stage > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Back"))
            {
                stage--;
                validationMessage = string.Empty;
                return;
            }
        }

        ImGui.SameLine();
        if (stage < StageCount - 1)
        {
            if (ImGui.Button("Next"))
            {
                if (ValidateStage(stage, out validationMessage))
                {
                    stage++;
                    validationMessage = string.Empty;
                }
            }
        }
        else if (ImGui.Button("Finish"))
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (!ValidateAll(out validationMessage))
            return;

        var account = plugin.ConfigManager.GetCurrentAccount();
        if (account == null || draft == null || draft.PresetIndex < 0 || draft.PresetIndex >= account.Presets.Count)
        {
            validationMessage = "The destination preset is no longer available. Reopen the wizard and try again.";
            return;
        }

        var preset = account.Presets[draft.PresetIndex];
        var newLine = BuildLine(draft);
        if (IsUntouchedDefaultExample(preset))
            preset.Lines[0] = newLine;
        else
            preset.Lines.Add(newLine);

        account.SelectedPresetIndex = draft.PresetIndex;
        if (mode == SetupWizardMode.Setup)
            account.Enabled = draft.EnableAccount;

        plugin.ConfigManager.SaveCurrentAccount();
        Plugin.Log.Info($"[HFH] Guided wizard saved one rule to preset index {draft.PresetIndex}");
        DiscardAndClose();
    }

    private bool ValidateAll(out string message)
    {
        for (var currentStage = 0; currentStage < StageCount - 1; currentStage++)
        {
            if (!ValidateStage(currentStage, out message))
                return false;
        }

        message = string.Empty;
        return true;
    }

    private bool ValidateStage(int stageToValidate, out string message)
    {
        message = string.Empty;
        if (draft == null)
        {
            message = "The setup draft is unavailable.";
            return false;
        }

        if (stageToValidate == 0)
        {
            var account = plugin.ConfigManager.GetCurrentAccount();
            if (account == null || draft.PresetIndex < 0 || draft.PresetIndex >= account.Presets.Count)
            {
                message = "Choose an available destination preset.";
                return false;
            }
        }

        if (stageToValidate == 1)
        {
            if (draft.SpecificPlayer && string.IsNullOrWhiteSpace(draft.TargetName))
            {
                message = "Enter a player name or choose Any nearby player.";
                return false;
            }

            if (draft.Trigger == SetupWizardTrigger.IncomingEmote && string.IsNullOrWhiteSpace(draft.TriggerEmote))
            {
                message = "Choose the incoming emote that should trigger this rule.";
                return false;
            }
        }

        if (stageToValidate == 2 &&
            draft.Trigger != SetupWizardTrigger.Copycat &&
            string.IsNullOrWhiteSpace(draft.ResponseCommand))
        {
            message = "Enter a response command.";
            return false;
        }

        if (stageToValidate == 3)
        {
            if (draft.WaitSeconds < 0 || draft.CooldownSeconds <= 0)
            {
                message = "Wait must be zero or more, and cooldown must be greater than zero.";
                return false;
            }

            if (draft.Trigger == SetupWizardTrigger.Proximity && draft.ProximityRange <= 0)
            {
                message = "Proximity range must be greater than zero.";
                return false;
            }

            if (draft.Trigger != SetupWizardTrigger.Proximity && draft.EmoteRange <= 0)
            {
                message = "Incoming-emote range must be greater than zero.";
                return false;
            }
        }

        return true;
    }

    private static EmoteLine BuildLine(SetupWizardDraft source)
    {
        return new EmoteLine
        {
            TargetName = source.SpecificPlayer ? source.TargetName.Trim() : "*",
            SlashCommand = source.ResponseCommand.Trim(),
            WaitTimeAfter = source.WaitSeconds,
            RepeatInterval = source.CooldownSeconds,
            DistanceThreshold = source.ProximityRange,
            EmoteRange = source.EmoteRange,
            WeatherFilter = source.Weather,
            TriggerType = source.Trigger == SetupWizardTrigger.Proximity ? 0 : 1,
            TriggerEmote = source.Trigger switch
            {
                SetupWizardTrigger.IncomingEmote => source.TriggerEmote,
                SetupWizardTrigger.Copycat => "COPYCAT",
                _ => string.Empty,
            },
            TargetBeforeCommand = source.TargetBeforeCommand,
            GlowEnabled = source.GlowEnabled,
            GlowColor = source.GlowEnabled ? source.GlowColor : null,
        };
    }

    private static bool IsUntouchedDefaultExample(EmotePreset preset)
    {
        if (!string.Equals(preset.Name, "DEFAULT PRESET", StringComparison.Ordinal) || preset.Lines.Count != 1)
            return false;

        var line = preset.Lines[0];
        return line.TriggerType == 0 &&
               string.Equals(line.TargetName, "Example Player", StringComparison.Ordinal) &&
               string.Equals(line.SlashCommand, "/wave", StringComparison.Ordinal) &&
               line.WaitTimeAfter == 3.0f &&
               line.RepeatInterval == 5.0f &&
               line.DistanceThreshold == 5.0f &&
               line.EmoteRange == 10.0f &&
               string.Equals(line.WeatherFilter, "ALL", StringComparison.Ordinal) &&
               string.IsNullOrEmpty(line.TriggerEmote) &&
               line.TargetBeforeCommand &&
               !line.GlowEnabled &&
               line.GlowColor == null;
    }

    private string TriggerDescription()
    {
        return draft!.Trigger switch
        {
            SetupWizardTrigger.IncomingEmote => "Selected incoming emote",
            SetupWizardTrigger.Copycat => "COPYCAT incoming emotes",
            _ => "Player enters range",
        };
    }

    private static void DrawReviewRow(string label, string value)
    {
        ImGui.TextDisabled($"{label}:");
        ImGui.SameLine();
        ImGui.TextWrapped(value);
    }

    private static string StageTitle(int currentStage)
    {
        return currentStage switch
        {
            0 => "Destination and enablement",
            1 => "Trigger and audience",
            2 => "Response",
            3 => "Optional tuning",
            _ => "Review",
        };
    }

    private void DiscardAndClose()
    {
        draft = null;
        validationMessage = string.Empty;
        IsOpen = false;
    }

    public void Dispose()
    {
        draft = null;
    }
}
