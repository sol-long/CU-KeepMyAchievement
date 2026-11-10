# CU-KeepMyAchievement

[English](README.md) · **简体中文**

适用于 *Casualties: Unknown* 的 BepInEx 5 插件，用于保持 Steam 成就可用。

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

不带参数的 `kma` 输出用法。

## 工作方式

成就解锁由一个静态字段统一控制：`SteamManager.disableAchievements`。游戏只有两个方法写入它，
一个方法读取它。

| 写入方 | 触发条件 | 处理方式 |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | 任一调用该校验的控制台命令 | Harmony Prefix 返回 `false`，方法体不执行，因此标记不会被置位，随附的警告也不会打印 |
| `SaveSystem.TryLoadGame()` | 存档文件中保存了 `cheatedSave: true` | 读档完成后的 Postfix 中清除该标记。游戏自身的警告已经打印，插件不作处理 |

实际效果：

- 控制台命令不再禁用成就。
- 载入被标记的存档时，游戏自身的警告仍会出现一次；此后标记为 `false`，本局内成就正常解锁。
- 下一次正常存档写入的是 `cheatedSave: false`：`SaveSystem.SaveGame` 保存的正是该字段的当前值，
  文件因此由游戏自己完成清洗；在玩家存档之前，文件保持原值。
- 正常游玩过程中不写存档文件。`kma unmarksave` 是唯一会重写存档的路径，且采用原子替换。
- 有两处控制台信息不需要打开控制台即可见：每局首次读档时一行（`… working …`），
  每次清除被标记存档的锁时一行（`… cleared the achievement lock of this save …`）。

本插件不解锁成就，也不修改或屏蔽游戏内的任何提示。

## 实现说明

- 控制台校验采用 Harmony Prefix 返回 `false` 跳过整个方法体，而不是事后清零。事后清零仍会执行原方法，
  而原方法会打开控制台并输出警告；跳过方法体则使该状态从一开始就不存在。
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
