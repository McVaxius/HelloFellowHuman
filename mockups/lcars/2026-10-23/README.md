# Hello Fellow Human LCARS concepts

Approved LCARS design concepts. Implementation is selected for the repositories in Z:\myrepos.txt, with the rollout tracked in Z:\!todo\2026-10-22GOATFUCK0002.md. These images are design references, not runtime or test evidence.

- [Main window comparison](01-main-regular-compact.png)
- [Settings and supporting owners](02-settings-mini-and-auxiliary.png)
- [Exact source-checked prompts](PROMPTS.md)

The image links are the planned sheet filenames; root review publishes the final generated artwork. Regular/compact compare spacing at the same readable text size. A separate Mini, wizard, access loader or browser view stays a separate owner. Selected supporting views are shown at useful scale; other source windows and conditional states retain their existing layouts and actions, rather than each receiving an illegible thumbnail. Generated text and sample values are proposals, not test results.

## Source scope

Source commit: `53c854fb0656c703e1d55e0bb3d1e646d80599d6`. Existing dirty paths were inspected by count (0); none are included or changed by this mockup task.

The wizard is nonmodal. New-preset and different wizard steps are distinct reference states.

## Verified controls and owners

- HelloFellowHuman/Windows/ConfigWindow.cs:111 — Presets/Configuration tabs; header actions at 170; enabled/DTR/Krangle at 187.
- HelloFellowHuman/Windows/ConfigWindow.cs:365 — preset actions; name/export/import at 441; rule actions at 512; Proximity/Emote combo at 618.
- HelloFellowHuman/Windows/SetupWizardWindow.cs:160 — destination; trigger radios at 183; target radios at 199; applicable navigation at 348.

## Existing UI source inventory

- `HelloFellowHuman/Windows/ConfigWindow.cs`
- `HelloFellowHuman/Windows/SetupWizardWindow.cs`

## Shared appearance proposal

Auto / Picker / LCARS remain in that order, with LCARS third. Preserve native actions, IDs, saved scope and semantic status colours. Krangle obfuscates identifying text. Proposed shared UI tones default off; titlebar speaker and Settings use the same value, with no volume slider. Browser controls remain in the browser header; existing script-free owners do not gain unsupported timed fade. This appearance contract is approved for the allowlisted rollout; implementation and local verification remain separate from artwork approval.
