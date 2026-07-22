# Spectre

A replay mod for [A Dance of Fire and Ice](https://store.steampowered.com/app/977950/A_Dance_of_Fire_and_Ice/), built with Harmony and UnityModManager.

## Features

### Replay System

Record and replay keyboard inputs with full accuracy.

**Recording**
- Captures every keyboard event (press/release) with precise song-position timestamps
- Records hit context per floor: angle, overload, auto-status, no-fail protection, free-roam section
- Hit margin distribution and X-accuracy tracking
- Optional keyboard sound recording via microphone (saved alongside replay as `.wav`)
- Late-save, fail-save, auto-save modes
- Manual save before exiting a level

**Playback**
- Floor-by-floor replay with exact angle/auto/overload restoration
- Full-run and checkpoint-based playback
- Fast-forward to starting checkpoint
- Legacy hit detection engine (switchable)
- Data integrity verification on load (hash checks for floor path, speed, time, pitch, BPM)

**File Formats**

| Extension | Type | Encryption |
|-----------|------|------------|
| `.sprp` | Binary (v2 metadata) | Encrypted |
| `.psprp` | Binary (v2 metadata) | Plain |
| `.crpl2` | Compact binary | Encrypted |
| `.pcrpl2` | Compact binary | Plain |
| `.crpl` | JSON (legacy) | Encrypted |
| `.pcrpl` | JSON (legacy) | Plain |

Metadata captures: song/artist name, judge mode, hit margin limit, hold behavior, internal level name, loaded mods list, device ID, mod version, keyboard sound hash, floor/speed/time hashes, pitch, BPM, speed trail, quick pitch, no-fail state, and more.

### Effect Remover

Strips selected effects from levels on load by hooking `LevelData.Decode`.

Toggle groups:
- **Non-DLC**: Filters, Advanced Filters, Particles, Decorations, Backgrounds, Cameras, Repeat Events, Frame Rate, Hit Sounds
- **Planet**: Orbit, Scale, Radius
- **Track**: Animations, Positions, Moves, Colors
- **DLC**: Hold Sounds, Hide Icons
- **Misc**: Remove All Decorations (or keep conditional-tag-protected ones), Reset Track Opacity, Reset Track Animation, Reset Track Color, Set Camera Zoom

Override settings:
- `Remove All Decorations` — clears all decorations except those tagged by conditional events
- `Set Camera Zoom` — overrides camera zoom (100–1000)
- `Reset Track Animation/Color` — resets to standard fade/single color

Works in both game and editor; editor save buttons are disabled while effect removal is active (configurable).

### Key Remapping

Remap key codes at runtime via `Options.UI` tab. Useful for custom keyboard layouts or cross-platform input handling.

### Audio Recording

Record keyboard sounds via connected microphone. Volume and offset adjustable. Audio saved alongside replay file and validated by hash on playback.

## Installation

1. Install [UnityModManager](https://www.nexusmods.com/site/mods/21) for ADOFAI
2. Place `Spectre.dll` in `UnityModManager/ADofAI/Mods/Spectre/`
3. Launch the game and enable Spectre in the mod manager

## Configuration

`Configs.json` is auto-generated next to the DLL on first launch. All settings are exposed in the in-game UI (toggle from UnityModManager mod list).

Settings are organized into 6 tabs:
- **Save Settings** — auto-save, complete-save, late-save, fail-save, manual-save, backup, legacy engine, don't save when auto/miss
- **Replaying Settings** — playback speed, save button position, key limit
- **Audio Record Settings** — keyboard sound recording, volume, microphone device, offset
- **Mod UI** — text size, language, key remapping
- **Debug Settings** — key validation, debug mode, skip verification
- **Effect Remover** — per-effect toggles as described above

## Building

- .NET Framework 4.8.1, C# 12
- Requires `../../adofai-libs/` with the game's DLLs
- Build with Visual Studio or MSBuild
- Output: `Spectre/bin/{Debug|Release}/Spectre.dll`

## Mod Compilation

Replay metadata records the full list of loaded mods at recording time. This information is displayed in the replay details panel and can be used for debugging compatibility issues.

## License

MIT

## References

- [YqlossClientHarmony](https://github.com/Necron-Dev/YqlossClientHarmony) — replay format reference and Harmony patching utilities
- [Creplay-mod](https://github.com/potatoonadofai/Creplay-mod) — replay mod for ADOFAI, feature and format reference
