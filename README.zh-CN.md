# CU-KeepMyAchievement

[English](README.md) · **简体中文**

适用于 *Casualties: Unknown* 的 BepInEx 5 插件，用于保持 Steam 成就可用。

## 工作方式

成就解锁由一个静态字段统一控制：`SteamManager.disableAchievements`。游戏中只有两个方法写入该字段，
只有一个方法读取它。

| 写入方 | 触发条件 | 处理方式 |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | 任一调用该校验的控制台命令 | Harmony Prefix 返回 `false`，原方法体不执行 |
| `SaveSystem.TryLoadGame()` | 存档文件中的 `cheatedSave` 为 `true` | 读档完成后在内存中清除该标记；游戏在下一次存档时把 `false` 写回文件 |

两处写入均被阻止后，唯一的读取方 `SteamManager.TryUnlockAchievement()` 不会读到已置位的标记。
游戏自身的判断逻辑未作任何修改。

本插件不解锁成就，不修改也不屏蔽游戏内的任何提示，正常游玩过程中不写入存档文件；
磁盘上的存档清洗是一个显式动作。

## 安装

依赖：游戏版本 **1.0**（Steam buildid `26525321`），以及 BepInEx 5.4.23.x（x64，Mono）。

1. 将 `CU-KeepMyAchievement.dll` 复制到 `<游戏目录>\BepInEx\plugins\CU-KeepMyAchievement\`。
2. 启动游戏，`BepInEx\LogOutput.log` 中应出现 `CU-KeepMyAchievement v0.1.1 loaded.`。

插件没有配置文件。删除 dll 即可恢复游戏原始行为。

## 控制台命令

按 `` ` `` 打开游戏内控制台，输入：

| 命令 | 说明 |
|---|---|
| `kma status` | 输出插件版本、已 patch 的方法数量、`disableAchievements` 的当前值、`SaveSystem.HasSave()` 的结果，以及每个存档文件中保存的标记值 |
| `kma unmarksave` | 强制清除存档目录下所有 `*.sv` 文件中的标记。文件以原子替换方式改写，不生成备份文件 |

> **使用 `kma unmarksave` 前请自行备份存档目录。** 该命令会就地重写存档文件，插件不保留副本。

不带参数的 `kma` 输出用法。每局游戏首次读档时，插件还会向控制台历史追加一行确认信息
（`CU-KeepMyAchievement: working …`），但不会主动打开控制台。

## 实现说明

- 该闸门是一个静态布尔字段 `SteamManager.disableAchievements`。写入它的是
  `ConsoleScript.CheckForCheats()` 与 `SaveSystem.TryLoadGame()`，读取它的是
  `SteamManager.TryUnlockAchievement()`。
- 控制台校验采用 Harmony Prefix 返回 `false` 跳过整个方法体，而不是事后清零。事后清零仍会执行原方法，
  而原方法会打开控制台并输出警告；跳过方法体则使该状态从一开始就不存在。
- **读档不写文件。** `TryLoadGame` 会置位并给出提示，插件在其 Postfix 中把该位清掉。
  `SaveSystem.SaveGame` 序列化的正是这个字段，因此游戏下一次存档写入的就是 `false`，
  文件由游戏自己的代码完成清洗；在玩家存档之前，文件保持原值。
- **`kma unmarksave` 是唯一会写存档文件的路径。** 它用游戏的 `SaveSystem.Zip` 重新打包，
  对**从磁盘读回**的字节做校验，写到存档旁的临时文件后用 `MoveFileEx` 原子替换，最后回读结果；
  失败则用保存在内存里的原始字节还原。本插件不保留存档的备份副本。
- 该标记属于进程级状态，每次启动均为 `false`。由于游戏每次存档都会写入它，
  只要在内存中清除，下一次存档自然就是干净的。

## 构建

依赖：Windows 自带的 .NET Framework 4.x 编译器，无需 SDK、NuGet 包或网络。

```powershell
pwsh -File build.ps1 -GameDir 'X:\Steam\steamapps\common\Casualties Unknown'
```

`build.ps1` 会从仓库内或与仓库同级的 `Casualties Unknown` 目录解析游戏路径，可用 `-GameDir` 覆盖。
加 `-Deploy` 会把编译产物复制到游戏的 `BepInEx\plugins` 目录。
