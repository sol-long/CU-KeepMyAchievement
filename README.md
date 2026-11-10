# CU-KeepMyAchievement

**English** · [简体中文](README.zh-CN.md)

A BepInEx 5 plugin for *Casualties: Unknown* that keeps Steam achievements enabled. It works as
soon as it is installed.

## Installation

Requirements: game version **1.0** (Steam buildid `26525321`), and BepInEx 5.4.23.x (x64, Mono).

1. Copy `CU-KeepMyAchievement.dll` into `<Game>\BepInEx\plugins\CU-KeepMyAchievement\`.
2. Launch the game. `BepInEx\LogOutput.log` must contain the line
   `CU-KeepMyAchievement v0.1.1 loaded.`

## Commands (optional)

Open the in-game console with `` ` `` and enter:

| Command | Description |
|---|---|
| `kma status` | Reports the plugin version, the number of patched methods, the current value of `disableAchievements`, the result of `SaveSystem.HasSave()`, and the marker stored in every save file |
| `kma unmarksave` | Forces the marker out of every `*.sv` file of the save directory. Files are replaced atomically; no backup copy is created |

> **Back up your save directory before using `kma unmarksave`.** The command rewrites save files in
> place and the plugin does not keep copies of them.

`kma` without arguments prints usage.

## How it works

Achievement unlocking is gated by a single static field, `SteamManager.disableAchievements`.
The game sets it from exactly two methods and reads it from one.

| Written by | Condition | Handling |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | Any console command that calls this check | A Harmony prefix returns `false`; the method body never runs, so the flag is never set and the accompanying warning is never printed |
| `SaveSystem.TryLoadGame()` | The save file stores `cheatedSave: true` | The flag is cleared in a postfix immediately after loading. The game's warning has already been printed and is left untouched |

What this means in practice:

- Console commands do not disable achievements at all.
- Loading a flagged save still prints the game's own warning once. From that point on the flag is
  `false`, so achievements unlock normally for the rest of the session.
- The next normal save stores `cheatedSave: false`: `SaveSystem.SaveGame` writes the current value
  of the flag, so the file is cleaned by the game itself. Until a save happens the file keeps its
  previous value.
- No save file is written during play. `kma unmarksave` is the only path that rewrites saves, and it
  does so atomically.
- Two console lines report activity without opening the console: one per session when the first
  save is loaded (`… working …`), and one each time a flagged save's lock is cleared
  (`… cleared the achievement lock of this save …`).

The plugin does not unlock achievements and does not modify or suppress any in-game message.

## Implementation notes

- The console check is bypassed with a Harmony prefix returning `false`, rather than by resetting
  the flag afterwards. Resetting would still execute the original method, which opens the console
  and emits a warning; skipping the method body prevents that state from being created at all.
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
