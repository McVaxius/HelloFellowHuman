using Dalamud.Bindings.ImGui;
using AethertekUI;
using AethertekUI.Dalamud;
using HelloFellowHuman.Ui;
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
    private readonly MaterialWindowMotion windowMotion = new();
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
        Flags |= ImGuiWindowFlags.HorizontalScrollbar;
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

    public override void PreDraw() => windowMotion.Prepare(this, reducedMotion: false, roundedCorners: true);

    public override void PostDraw() => windowMotion.Restore(this);

    public override void Draw()
    {
        windowMotion.DrawChrome();
        UiGui.Title("Hello Fellow Human Guided Setup", "Hello Fellow Human — " + UiText.T("Guided Setup"));
        var account = plugin.ConfigManager.GetCurrentAccount();
        if (draft == null || account == null || account.Presets.Count == 0)
        {
            UiGui.TextWrapped("Log in and select an account before using guided setup.");
            if (UiGui.Button("Close"))
                DiscardAndClose();
            return;
        }

        UiGui.Text(mode == SetupWizardMode.Setup ? "Guided Setup" : "Add Rule with Wizard");
        ImGui.SameLine();
        UiGui.TextDisabled(UiText.Interpolated($"- Step {stage + 1} of {StageCount}"));
        ImGui.ProgressBar((stage + 1) / (float)StageCount, new Vector2(-1, 0), UiText.T(StageTitle(stage)));
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
            UiGui.TextColored(new Vector4(1.0f, 0.35f, 0.35f, 1.0f), validationMessage);
        }

        DrawNavigation();
    }

    private void DrawDestinationStage(AccountConfig account)
    {
        UiGui.TextWrapped("Choose the preset that will receive this rule. Nothing is saved until Finish.");
        ImGui.Spacing();

        var presetNames = new string[account.Presets.Count];
        for (var i = 0; i < account.Presets.Count; i++)
            presetNames[i] = $"[{i}] {account.Presets[i].Name}";

        ImGui.SetNextItemWidth(-1);
        var presetIndex = draft!.PresetIndex;
        if (UiGui.Combo("Destination preset", ref presetIndex, presetNames, presetNames.Length, presetNames))
            draft.PresetIndex = presetIndex;

        ImGui.Spacing();
        if (mode == SetupWizardMode.Setup)
        {
            var enableAccount = draft.EnableAccount;
            if (UiGui.Checkbox("Enable automatic reactions when I finish", ref enableAccount))
                draft.EnableAccount = enableAccount;
            UiGui.TextDisabled("Clear this if you want to review the new rule in the advanced editor before enabling it.");
        }
        else
        {
            var status = UiText.T(account.Enabled ? "Enabled" : "Disabled");
            UiGui.TextWrapped(UiText.F("This account is currently {0}. Add Rule mode will not change that setting.", status));
        }
    }

    private void DrawTriggerStage()
    {
        UiGui.TextWrapped("Choose what starts the reaction.");
        ImGui.Spacing();

        if (UiGui.RadioButton("A player enters range", draft!.Trigger == SetupWizardTrigger.Proximity))
            draft.Trigger = SetupWizardTrigger.Proximity;
        if (UiGui.RadioButton("A selected emote is performed", draft.Trigger == SetupWizardTrigger.IncomingEmote))
            draft.Trigger = SetupWizardTrigger.IncomingEmote;
        if (UiGui.RadioButton("COPYCAT any incoming emote", draft.Trigger == SetupWizardTrigger.Copycat))
            draft.Trigger = SetupWizardTrigger.Copycat;

        if (draft.Trigger == SetupWizardTrigger.IncomingEmote)
        {
            ImGui.Spacing();
            DrawIncomingEmoteSelector();
        }

        ImGui.Spacing();
        ImGui.Separator();
        UiGui.Text("Who can trigger it?");
        if (UiGui.RadioButton("Any nearby player", !draft.SpecificPlayer))
            draft.SpecificPlayer = false;
        if (UiGui.RadioButton("One specific player", draft.SpecificPlayer))
            draft.SpecificPlayer = true;

        if (draft.SpecificPlayer)
        {
            var targetName = draft.TargetName;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.InputText("Player name (without @World)", ref targetName, 100))
                draft.TargetName = targetName;
        }
    }

    private void DrawIncomingEmoteSelector()
    {
        var preview = string.IsNullOrWhiteSpace(draft!.TriggerEmote) ? "Select an emote" : draft.TriggerEmote;
        ImGui.SetNextItemWidth(-1);
        if (!UiGui.BeginCombo("Incoming emote", preview))
            return;

        foreach (var emote in plugin.EmoteDetectionService.EmoteCommands)
        {
            if (string.Equals(emote, "COPYCAT", StringComparison.Ordinal))
                continue;

            var selected = string.Equals(emote, draft.TriggerEmote, StringComparison.Ordinal);
            if (MaterialText.Selectable(emote, selected))
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
            UiGui.TextWrapped("COPYCAT mirrors the incoming emote. You may add a fallback command for an emote that cannot be mirrored or is already looping.");
            ImGui.Spacing();
            var fallback = draft.ResponseCommand;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.InputText("Optional fallback command", ref fallback, 200))
                draft.ResponseCommand = fallback;
            UiGui.TextDisabled("Leave this blank for no fallback.");
        }
        else
        {
            UiGui.TextWrapped("Enter the command to run when this rule triggers.");
            ImGui.Spacing();
            var response = draft.ResponseCommand;
            ImGui.SetNextItemWidth(-1);
            if (UiGui.InputText("Response command", ref response, 200))
                draft.ResponseCommand = response;
            UiGui.TextDisabled("Example: /wave motion");
        }
    }

    private void DrawTuningStage()
    {
        UiGui.TextWrapped("These defaults are ready to use. Adjust only what you need.");
        ImGui.Spacing();

        var wait = draft!.WaitSeconds;
        ImGui.SetNextItemWidth(180);
        if (UiGui.DragFloat("Wait after response (seconds)", ref wait, 0.1f, 0.0f, 60.0f, "%.1f"))
            draft.WaitSeconds = wait;

        var cooldown = draft.CooldownSeconds;
        ImGui.SetNextItemWidth(180);
        if (UiGui.DragFloat("Cooldown (seconds)", ref cooldown, 0.1f, 0.1f, 300.0f, "%.1f"))
            draft.CooldownSeconds = cooldown;

        if (draft.Trigger == SetupWizardTrigger.Proximity)
        {
            var distance = draft.ProximityRange;
            ImGui.SetNextItemWidth(180);
            if (UiGui.DragFloat("Proximity range (yalms)", ref distance, 0.1f, 0.1f, 100.0f, "%.1f"))
                draft.ProximityRange = distance;
        }
        else
        {
            var emoteRange = draft.EmoteRange;
            ImGui.SetNextItemWidth(180);
            if (UiGui.DragFloat("Incoming-emote range (yalms)", ref emoteRange, 0.1f, 0.1f, 100.0f, "%.1f"))
                draft.EmoteRange = emoteRange;
        }

        var weatherTypes = WeatherService.GetWeatherTypes();
        var weatherIndex = weatherTypes.IndexOf(draft.Weather);
        if (weatherIndex < 0)
            weatherIndex = 0;
        ImGui.SetNextItemWidth(260);
        if (UiGui.Combo("Required weather", ref weatherIndex, weatherTypes.ToArray(), weatherTypes.Count))
            draft.Weather = weatherTypes[weatherIndex];

        var targetBeforeCommand = draft.TargetBeforeCommand;
        if (UiGui.Checkbox("Target the triggering player before the command", ref targetBeforeCommand))
            draft.TargetBeforeCommand = targetBeforeCommand;

        var glowEnabled = draft.GlowEnabled;
        if (UiGui.Checkbox("Show a temporary nameplate glow", ref glowEnabled))
            draft.GlowEnabled = glowEnabled;

        if (draft.GlowEnabled)
        {
            var glowColor = draft.GlowColor;
            if (UiGui.ColorEdit3("Glow color", ref glowColor))
                draft.GlowColor = glowColor;
        }
    }

    private void DrawReviewStage(AccountConfig account)
    {
        var presetName = draft!.PresetIndex >= 0 && draft.PresetIndex < account.Presets.Count
            ? account.Presets[draft.PresetIndex].Name
            : UiText.T("Unavailable");

        UiGui.TextWrapped("Review the rule below. Finish applies it and saves once; Cancel or closing this window leaves configuration unchanged.");
        ImGui.Spacing();
        DrawReviewRow("Preset", presetName);
        if (mode == SetupWizardMode.Setup)
            DrawReviewRow("Account after finish", UiText.T(draft.EnableAccount ? "Enabled" : "Disabled"));
        DrawReviewRow("Trigger", UiText.T(TriggerDescription()));
        DrawReviewRow("Audience", draft.SpecificPlayer ? draft.TargetName.Trim() : UiText.T("Any nearby player"));
        if (draft.Trigger == SetupWizardTrigger.IncomingEmote)
            DrawReviewRow("Incoming emote", draft.TriggerEmote);
        DrawReviewRow(
            draft.Trigger == SetupWizardTrigger.Copycat ? "Fallback" : "Response",
            string.IsNullOrWhiteSpace(draft.ResponseCommand) ? UiText.T("None") : draft.ResponseCommand.Trim());
        DrawReviewRow("Timing", UiText.Interpolated($"Wait {draft.WaitSeconds:0.0}s; cooldown {draft.CooldownSeconds:0.0}s"));
        DrawReviewRow(
            "Range",
            draft.Trigger == SetupWizardTrigger.Proximity
                ? UiText.Interpolated($"{draft.ProximityRange:0.0} yalms")
                : UiText.Interpolated($"{draft.EmoteRange:0.0} yalms"));
        DrawReviewRow("Weather", UiText.T(draft.Weather == "ALL" ? "Any weather" : draft.Weather));
        DrawReviewRow("Target first", UiText.T(draft.TargetBeforeCommand ? "Yes" : "No"));
        DrawReviewRow("Nameplate glow", UiText.T(draft.GlowEnabled ? "On" : "Off"));
    }

    private void DrawNavigation()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (UiGui.Button("Cancel"))
        {
            DiscardAndClose();
            return;
        }

        if (stage > 0)
        {
            UiGui.SameLineIfFits(UiGui.ButtonWidth("Back"));
            if (UiGui.Button("Back"))
            {
                stage--;
                validationMessage = string.Empty;
                return;
            }
        }

        UiGui.SameLineIfFits(Math.Max(UiGui.ButtonWidth("Next"), UiGui.ButtonWidth("Finish")));
        if (stage < StageCount - 1)
        {
            if (UiGui.Button("Next"))
            {
                if (ValidateStage(stage, out validationMessage))
                {
                    stage++;
                    validationMessage = string.Empty;
                }
            }
        }
        else if (UiGui.Button("Finish"))
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
        MaterialText.TextDisabled(UiText.T(label) + ":");
        ImGui.SameLine();
        MaterialText.TextWrapped(value);
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
