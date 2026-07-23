# Hello Fellow Human

Build proximity and emote-triggered reactions for nearby players in FINAL FANTASY XIV. Hello Fellow Human lets you create presets of automatic social or roleplay responses without replacing the game’s normal emote and command systems.

[Visit Aethertek for plugins and guides](https://aethertek.io/) · [Support development on Ko-fi](https://ko-fi.com/mcvaxius)

## Installation

Add this custom repository in Dalamud:

```text
https://aethertek.io/x.json
```

Then install **Hello Fellow Human** from the plugin installer.

## Guided quick start

1. Run `/hfh wizard` or `/hfh setup`, or select **Guided Setup** in the config window.
2. Choose the destination preset and whether Setup mode should enable the account when finished.
3. Choose a proximity trigger, one incoming emote, or `COPYCAT`, then choose any nearby player or a specific player.
4. Enter the response command. A `COPYCAT` rule can leave its fallback command blank.
5. Review timing, cooldown, range, weather, targeting, and optional nameplate glow, then select **Finish**.

The wizard keeps its work in a draft. Back preserves the draft, while Cancel or closing the window leaves the configuration unchanged. Finish adds one editable rule to the chosen preset. The untouched `DEFAULT PRESET` example is replaced instead of leaving an extra sample row.

## Features

- **Proximity reactions:** Respond when a specific player or any nearby player enters a configurable range.
- **Incoming-emote reactions:** Respond when a selected emote is performed nearby, with a separate emote-detection range.
- **COPYCAT:** Mirror incoming emotes and optionally use a fallback command when an emote cannot be mirrored or is already looping.
- **Presets:** Keep multiple rule sets, switch the active preset, and import or export presets as base64 text.
- **Per-rule tuning:** Configure wait time, cooldown, proximity or emote range, weather, and whether to target the triggering player before running the command.
- **Optional nameplate glow:** Apply a temporary color effect to the triggering player when a rule runs.
- **Advanced media commands:** Advanced rules can use `media:`, `video:`, `audio:`, or `sound:` to launch a local file by full path or relative to the plugin config folder.
- **DTR integration:** Show plugin status and the active preset, and click the entry to toggle the current account on or off.
- **Guided and advanced editing:** Use the wizard for a plain-language setup or the existing table editor for direct control of every rule.

## Commands

- `/hfh` — Toggle the advanced config window.
- `/hfh wizard` or `/hfh setup` — Open Guided Setup.
- `/hfh on` or `/hfh enable` — Enable reactions for the current account.
- `/hfh off` or `/hfh disable` — Disable reactions for the current account.
- `/hfh preset <id>` — Switch the active preset by its displayed numeric ID.

## Advanced editor

The **Presets** tab keeps the existing table editor for users who want direct control. Select a preset to make it active, use **Add Blank Rule** for an empty row, or use **Add Rule with Wizard** to build a validated row for that preset.

Each rule can define:

- proximity or incoming-emote trigger type;
- any-player or specific-player audience;
- response command and, for `COPYCAT`, an optional fallback;
- wait time and cooldown;
- proximity distance or incoming-emote range;
- weather requirement;
- target-before-command behavior; and
- optional nameplate glow and color.

Invalid rows appear in red and are ignored by the runtime engine until corrected. The advanced editor also provides preset import/export and a reset action for `DEFAULT PRESET`.

## Safety and scope

Hello Fellow Human runs configured reactions through the existing plugin rule engine. Local media commands open files on the same computer, so use only paths and preset imports you trust. Configuration is stored per account; guided Add Rule mode never changes account enablement.
