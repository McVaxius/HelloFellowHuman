# Hello Fellow Human UI/UX Recommendations

**Review date:** 2026-08-18  
**Scope:** UI code review only; no runtime behaviour or implementation changes are included in this document.

## Product goal

Create understandable, safe reaction rules and know exactly who can trigger which command under which conditions.

## Reviewed surfaces

- `HelloFellowHuman/Windows/ConfigWindow.cs`
- `HelloFellowHuman/Windows/SetupWizardWindow.cs`

## What is already working

- The guided setup stages destination, trigger, response, tuning, and review before saving.
- Presets support create, import/export, targeting, cooldown, weather, glow, and COPYCAT behaviour.
- The wizard explicitly states that cancel/close leaves configuration unchanged.

## Prioritized recommendations

| Priority | Recommendation | Rationale and completion signal |
| --- | --- | --- |
| P0 | Summarize every rule as a sentence. | Use a live statement such as `When any nearby player waves within 5y, target them and run /wave motion, at most once every 30s` in the editor, list, and final review. |
| P0 | Keep global state in one persistent header. | Enabled, DTR, and Krangle currently appear in multiple tabs. Show them once above Presets/Configuration so values never appear duplicated or out of sync. |
| P0 | Make trigger scope and command risk explicit. | Visually distinguish any player from a named player, show the effective target, and warn before saving commands with broad proximity or COPYCAT triggers. |
| P1 | Show validation beside the field. | Invalid player names, missing commands, unsupported emotes, cooldown conflicts, and fallback issues should appear inline rather than only when advancing. |
| P1 | Protect preset edits with draft state. | Show Unsaved changes, Save/Revert, and the active preset clearly, especially when editing the default preset versus another preset. |
| P1 | Replace Ctrl-to-delete as the only guard. | Use a confirmation naming the preset and its rule count; optionally retain Ctrl as a power-user shortcut. |
| P2 | Preview imports before replacing or merging. | List incoming presets, duplicate names, conflicts, and the chosen resolution before applying imported data. |

## Suggested information hierarchy

1. Global state
2. Preset list
3. Plain-language rule summary
4. Guided/basic editor
5. Advanced conditions and import/export

## Validation checklist

- A new user can identify the primary action and current blocker within five seconds.
- Every disabled control has a nearby plain-language reason and, when possible, a direct corrective action.
- Healthy, warning, error, running, and disabled states remain distinguishable without colour.
- The UI remains usable at narrow window widths and common Dalamud UI scales without clipped labels or unreachable controls.
- Destructive, global, or high-impact actions identify their scope and require confirmation or provide a safe undo.
- Empty, loading, stale-data, success, partial-success, and failure states each provide an appropriate next action.
- Settings clearly identify whether they apply globally, per account, per character, per preset, or only for the current session.
- Advanced diagnostics are still reachable but do not compete with the everyday workflow.

## Recommended implementation order

1. Implement P0 items and validate the primary workflow plus blocker recovery.
2. Implement P1 information-architecture and configuration improvements.
3. Apply P2 polish, then test at multiple UI scales with both fresh and mature configurations.
