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

## Appearance and languages

**Transparency** applies to the complete plugin window, including its titlebar and popups. Configuration provides normal opacity, automatic focus fade, faded opacity and delay; defaults are 100%, fading to 50% after 10 seconds without focus and restoring on focus. Compact and language controls can be hidden independently on Main while remaining available in Configuration.

The editor's titlebar opens Configuration or Guided Setup and toggles reactions for the current account. Configuration expands the editor and selects its existing tab; Setup keeps the selected preset workflow. The packaged plugin icon appears in editor branding and its titlebar, including when collapsed.

The main editor follows the approved regular and compact layouts, with peach-accented preset cards, separate rules, and the packaged plugin icon. The header's **C** checkbox shares compact spacing with Configuration, Guided Setup and popups. Colour and language selectors are available in the header and Configuration. English, German, French, Spanish, Italian, Russian, Japanese, Korean, Simplified Chinese, Vietnamese, Brazilian Portuguese, Indonesian, Polish, Turkish and Hindi are included; UI numbers use the selected language's formatting. Commands, player/preset names and stored weather values keep their original data.

The colour selector offers teal, blue, pink and custom RGB. Backgrounds, panels, fields, borders and decorative text follow the selected colour through relative OKLCH roles. Active-preset green, invalid-rule red and deletion warnings retain their meanings. Preferences use the existing global configuration and save path; account rules remain in their existing per-account files.

Segoe UI managed fonts merge host-provided CJK and symbol glyphs. Font files remain on the host. The plugin distribution includes AethertekUI.dll and fifteen keyed embedded catalogs. GitHub builds obtain the private library through the existing sibling checkout and AETHERTEKUI_DEPLOY_KEY workflow secret.

Hindi is enabled only when the local font check passes. Otherwise the selector shows disabled **Hindi (unavailable)** while other languages remain usable. A saved Hindi choice that fails its required-font check shows an English status and **Use English**; that button explicitly saves English. Font failures never change the saved language automatically.

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

All 13 rule columns remain available in the horizontally scrolling grid: Type, ALL, ToT, Name, Command, Wait, Repeat, Dist/Emote, Weather, Emote Range, Glow, Color and Remove. Names remain read-only when ALL or Krangle requires it. Preset names are a read-only display; select **New Preset** to create a named copy. Deleting a user preset requires holding Ctrl, and the built-in preset remains protected. Selecting a preset still activates it and resets its runtime cooldowns when the active preset changes.

Invalid rows appear in red and are ignored by the runtime engine until corrected. The advanced editor also provides preset import/export and a reset action for `DEFAULT PRESET`.

## Safety and scope

Hello Fellow Human runs configured reactions through the existing plugin rule engine. Local media commands open files on the same computer, so use only paths and preset imports you trust. Configuration is stored per account; guided Add Rule mode never changes account enablement.

## Local verification

The approved regular reference is 1505 × 1045, with a 1471 × 1010 window envelope, a 90 px header and 70 px tab row. Its preset pane is 285 × 800 and editor pane 1139 × 802, separated by 15 px. The compact reference is 1506 × 1045, with a 1450 × 563 envelope, a 78 px header, 68 px tab row and approximately 391 px panes. The reference accent is #FEB995; panel, field and border samples are #19232B, #242E37 and #303942. Title roles are 30/28 px, pane headings 22/20 px and body 16 px. These values are maintained in the consumer presentation constants.

Native window chrome, saved window dimensions and real rule values govern the implemented layout. The source has numeric emote range rather than the reference's illustrative “Self” value, and no row reordering or preset search. The five-stage wizard retains its existing layout and draft workflow with shared fonts, colours, language and spacing.

Local checks cover Debug and Release builds, resource values/placeholders/UTF-8, embedded resources and release ZIP contents. The current offline native editor check also covers all fourteen languages, regular/compact spacing, 100%/150% scales, narrow/reference widths, absent/selected accounts, native tab navigation, and complete tab/header/footer text with the six original font roles. The retained close controls, name-row K action and wrapped preset footer keep their native identities and readable bounds. Complete reference comparisons, remaining grid/popup/wizard interactions, actual managed-atlas readiness, custom/neutral theme appearance and in-game visual acceptance remain open. No clients were started or updated for these checks.

## Support logs

Use **Copy / ZIP Dalamud log** in Configuration > Configuration to create a local ZIP and open its folder. At 100 MiB or above, the first click warns that logging may have stopped and recent activity may be missing; click **Export capped log anyway** only if you still want that snapshot. Share the ZIP manually and remove exports when no longer needed. **Open Export Folder** reopens the completed export’s folder.

When XA Slave is loaded, **Open XA Slave log tools** opens its **Utility > XA Mods** panel, which contains Dalamud Log Cleaner. The existing **Copy / ZIP Dalamud log** action remains separate. Opening the panel does not run cleanup or change XA Slave settings.

Compact mode defaults on. The main Compact and Transparency controls start hidden; Appearance settings keeps density, transparency and independent main-control visibility choices. The one-time migration preserves opacity and unrelated preferences, and later loads retain your choices.
