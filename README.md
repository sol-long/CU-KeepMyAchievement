# CU-KeepMyAchievement

**English** · [简体中文](README.zh-CN.md)

A BepInEx 5 plugin for *Casualties: Unknown* that keeps Steam achievements enabled.

## How it works

Achievement unlocking is gated by a single static field, `SteamManager.disableAchievements`.
The game writes this field from exactly two methods and reads it from one.

| Written by | Condition | Handling |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | Any console command that calls this check | A Harmony prefix returns `false`; the original method body is not executed |
| `SaveSystem.TryLoadGame()` | The `cheatedSave` field of the save file is `true` | The field is cleared in memory immediately after loading; the game writes `false` back into the file on the next save |

Because both writers are neutralised, `SteamManager.TryUnlockAchievement()` — the only reader —
never observes a set flag. The game's own decision logic is not modified.

The plugin does not unlock achievements, does not alter or suppress any in-game message, and does
not write to save files during normal play. Cleaning a save file on disk is an explicit action.

## Installation

Requirements: game version **1.0** (Steam buildid `26525321`), and BepInEx 5.4.23.x (x64, Mono).

1. Copy `CU-KeepMyAchievement.dll` into `<Game>\BepInEx\plugins\CU-KeepMyAchievement\`.
2. Launch the game. `BepInEx\LogOutput.log` must contain the line
   `CU-KeepMyAchievement v0.1.1 loaded.`

The plugin has no configuration file. Removing the dll restores the unmodified behaviour.

## Console commands

Open the in-game console with `` ` `` and enter:

| Command | Description |
|---|---|
| `kma status` | Reports the plugin version, the number of patched methods, the current value of `disableAchievements`, the result of `SaveSystem.HasSave()`, and the marker stored in every save file |
| `kma unmarksave` | Forces the marker out of every `*.sv` file of the save directory. Files are replaced atomically; no backup copy is created |

> **Back up your save directory before using `kma unmarksave`.** The command rewrites save files in
> place and the plugin does not keep copies of them.

`kma` without arguments prints usage. On the first load of a session the plugin also appends one
confirmation line to the console history (`CU-KeepMyAchievement: working …`) without opening the
console.

## Implementation notes

- The gate is a single `static bool`, `SteamManager.disableAchievements`. It is assigned in
  `ConsoleScript.CheckForCheats()` and `SaveSystem.TryLoadGame()`, and read in
  `SteamManager.TryUnlockAchievement()`.
- The console check is bypassed with a Harmony prefix returning `false`, rather than by resetting
  the flag afterwards. Resetting would still execute the original method, which opens the console
  and emits a warning; skipping the method body prevents that state from being created at all.
- **Loading does not write files.** `TryLoadGame` sets the flag and reports it; the plugin clears
  the flag in a postfix. `SaveSystem.SaveGame` serialises the same field, so the next save stores
  `false` and the file is cleaned by the game's own code. Until a save happens the file keeps its
  previous value.
- **`kma unmarksave` is the only path that writes a save file.** It packs the edited document with
  the game's `SaveSystem.Zip`, verifies the bytes read back from disk, writes them to a temporary
  file beside the save and replaces the original atomically (`MoveFileEx`), then re-reads the
  result. The previous bytes are held in memory and written back if the result is not clean. The
  plugin keeps no backup copies of save files.
- The flag is scoped to the current process and starts as `false` on every launch. Because the
  game stores it on every save, clearing it in memory is enough to make the next save clean.

## Building

Requirements: the .NET Framework 4.x compiler included with Windows. No SDK, NuGet packages or
network access are required.

```powershell
pwsh -File build.ps1 -GameDir 'X:\Steam\steamapps\common\Casualties Unknown'
```

`build.ps1` resolves the game directory from a `Casualties Unknown` folder inside the repository
or next to it; `-GameDir` overrides the lookup. Pass `-Deploy` to copy the built assembly into the
game's `BepInEx\plugins` directory.
