# CU-KeepMyAchievement

**English** · [简体中文](README.zh-CN.md)

A BepInEx 5 plugin for *Casualties: Unknown* that keeps Steam achievements enabled.
In the unmodified game achievements are disabled in two situations: when a console command
that performs the cheat check is executed, and when a save file carrying the disable marker is
loaded. The plugin addresses both.

## How it works

Achievement unlocking is gated by a single static field, `SteamManager.disableAchievements`.
The game writes this field from exactly two methods and reads it from one.

| Written by | Condition | Handling |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | Any console command that calls this check | A Harmony prefix returns `false`; the original method body is not executed |
| `SaveSystem.TryLoadGame()` | The `cheatedSave` field of the save file is `true` | The field is rewritten to `false` before the file is read |

Because both writers are prevented, `SteamManager.TryUnlockAchievement()` — the only reader —
never observes a set flag. The game's own decision logic is not modified.

The plugin does not unlock achievements, does not alter or suppress any in-game message, and
does not modify any other field of the save file.

## Installation

Requirements: BepInEx 5.4.23.x (x64, Mono).

1. Copy `CU-KeepMyAchievement.dll` into `<Game>\BepInEx\plugins\CU-KeepMyAchievement\`.
2. Launch the game. `BepInEx\LogOutput.log` must contain the line
   `CU-KeepMyAchievement v0.1.0 loaded.`

The plugin has no configuration file. Removing the dll restores the unmodified behaviour.

## Console commands

Open the in-game console with `` ` `` and enter:

| Command | Description |
|---|---|
| `kma status` | Reports the plugin version, the number of patched methods, the current value of `disableAchievements`, the result of `SaveSystem.HasSave()`, and the marker stored in every save file |
| `kma unmarksave` | Clears the marker in every `*.sv` file of the save directory. Files are rewritten, and a single `*.keeper.bak` copy is retained per modified file |

Invoking `kma` without arguments prints usage.

## Implementation notes

- The gate is a single `static bool`, `SteamManager.disableAchievements`. It is assigned in
  `ConsoleScript.CheckForCheats()` and `SaveSystem.TryLoadGame()`, and read in
  `SteamManager.TryUnlockAchievement()`.
- The console check is bypassed with a Harmony prefix returning `false`, rather than by resetting
  the flag afterwards. Resetting would still execute the original method, which opens the console
  and emits a warning; skipping the method body prevents that state from being created at all.
- Save files are gzip-compressed JSON. The marker is rewritten through the game's own
  `SaveSystem.Unzip` and `SaveSystem.Zip`, so the container format is preserved by construction.
  Before writing, the packed bytes are unpacked and parsed again; the write is aborted if the
  field is not `false` or the JSON cannot be parsed.
- The flag is scoped to the current process and starts as `false` on every launch. The game
  writes its current value into the save file whenever it saves, so clearing the value stored in
  the file is what makes the change persistent.

## Building

Requirements: the .NET Framework 4.x compiler included with Windows. No SDK, NuGet packages or
network access are required.

```powershell
pwsh -File build.ps1 -GameDir 'X:\Steam\steamapps\common\Casualties Unknown'
```

`build.ps1` resolves the game directory from a `Casualties Unknown` folder inside the repository
or next to it; `-GameDir` overrides the lookup. Pass `-Deploy` to copy the built assembly into the
game's `BepInEx\plugins` directory.
