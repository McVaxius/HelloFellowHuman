# Hello Fellow Human

---

**Help fund my AI overlords' coffee addiction so they can keep generating more plugins instead of taking over the world**

[☕ Support development on Ko-fi](https://ko-fi.com/mcvaxius)

---
   ```
   https://raw.githubusercontent.com/McVaxius/TheDumpsterFire/refs/heads/master/repo.json
   ```
FFXIV Dalamud plugin for automated proximity-based emote reactions.

## Features

- **Preset System**: Create multiple emote presets with custom configurations
- **Distance-Based Triggers**: Emotes trigger when specific players are within range
- **Configurable Timing**: Set wait times and repeat intervals per emote line
- **DTR Bar Integration**: Click to toggle on/off, shows current status and active preset
- **Slash Commands**: Full command-line control
- **Import/Export**: Share presets via base64 encoding

## Usage

### Opening the Config
- `/hfh` - Toggle config window
- Right-click DTR bar entry

### Enabling/Disabling
- `/hfh on` or `/hfh enable` - Enable the plugin
- `/hfh off` or `/hfh disable` - Disable the plugin
- Left-click DTR bar entry - Toggle on/off

### Preset Management
- `/hfh preset <id>` - Switch to preset by cardinality (0, 1, 2, etc.)
- Use the config UI to create, delete, and edit presets

## Configuration

### Left Panel
- **New**: Create a new preset (uses DEFAULT PRESET as template)
- **Delete**: Hold CTRL and click to delete selected preset (DEFAULT PRESET cannot be deleted)
- **Preset List**: Click to select, shows cardinality [0], [1], etc.

### Right Panel
- **Export**: Copy preset to clipboard as base64
- **Import**: Paste base64 preset to load
- **Reset Default**: (DEFAULT PRESET only) Reset to factory defaults

### Emote Lines
Each line has 5 fields:
1. **Name**: Target player name (without @server)
2. **Emote**: Slash command to execute (e.g., `/wave`, `/bow`)
3. **Wait**: Seconds to wait after executing this emote
4. **Repeat**: Seconds before this emote can trigger again
5. **Distance**: Maximum distance (yalms) to trigger

Lines show in **red** if incomplete/invalid and won't be saved until properly filled.

## How It Works

1. Every second, the plugin checks all valid emote lines in the active preset
2. For each line, it checks if the target player is within distance
3. If the repeat interval has elapsed since last execution, the line becomes valid
4. When multiple lines are valid, one is chosen randomly
5. The plugin targets the player, executes the slash command, then waits
6. No other emotes execute until the wait time expires

## Technical Notes

- Uses `ICommandManager.ProcessCommand()` for slash commands (learned from FrenRider)
- Distance calculation via `Vector3.Distance()` between player positions
- Randomized execution order prevents predictable patterns
- Per-line cooldown tracking ensures repeat intervals are respected
- Global wait state prevents spam

## Version
0.0.0.1
