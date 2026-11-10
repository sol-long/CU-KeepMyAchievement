# CU-KeepMyAchievement

[English](README.md) · **简体中文**

适用于 *Casualties: Unknown* 的 BepInEx 5 插件，用于保持 Steam 成就可用。
未经修改的游戏会在两种情况下禁用成就：执行带有作弊校验的控制台命令时，以及载入带有禁用标记的存档时。
本插件分别处理这两种情况。

## 工作方式

成就解锁由一个静态字段统一控制：`SteamManager.disableAchievements`。游戏中只有两个方法写入该字段，
只有一个方法读取它。

| 写入方 | 触发条件 | 处理方式 |
|---|---|---|
| `ConsoleScript.CheckForCheats()` | 任一调用该校验的控制台命令 | Harmony Prefix 返回 `false`，原方法体不执行 |
| `SaveSystem.TryLoadGame()` | 存档文件中的 `cheatedSave` 为 `true` | 在读取文件之前把该字段改写为 `false` |

两处写入均被阻止后，唯一的读取方 `SteamManager.TryUnlockAchievement()` 不会读到已置位的标记。
游戏自身的判断逻辑未作任何修改。

本插件不解锁成就，不修改也不屏蔽游戏内的任何提示，不改动存档中的其它字段。

## 安装

依赖：BepInEx 5.4.23.x（x64，Mono）。

1. 将 `CU-KeepMyAchievement.dll` 复制到 `<游戏目录>\BepInEx\plugins\CU-KeepMyAchievement\`。
2. 启动游戏，`BepInEx\LogOutput.log` 中应出现 `CU-KeepMyAchievement v0.1.0 loaded.`。

插件没有配置文件。删除 dll 即可恢复游戏原始行为。

## 控制台命令

按 `` ` `` 打开游戏内控制台，输入：

| 命令 | 说明 |
|---|---|
| `kma status` | 输出插件版本、已 patch 的方法数量、`disableAchievements` 的当前值、`SaveSystem.HasSave()` 的结果，以及每个存档文件中保存的标记值 |
| `kma unmarksave` | 清除存档目录下所有 `*.sv` 文件中的标记。文件会被重写，每个被修改的文件保留一份 `*.keeper.bak` 副本 |

不带参数的 `kma` 输出用法。

## 实现说明

- 该闸门是一个静态布尔字段 `SteamManager.disableAchievements`。写入它的是
  `ConsoleScript.CheckForCheats()` 与 `SaveSystem.TryLoadGame()`，读取它的是
  `SteamManager.TryUnlockAchievement()`。
- 控制台校验采用 Harmony Prefix 返回 `false` 跳过整个方法体，而不是事后清零。事后清零仍会执行原方法，
  而原方法会打开控制台并输出警告；跳过方法体则使该状态从一开始就不存在。
- 存档为 gzip 压缩的 JSON。标记通过游戏自带的 `SaveSystem.Unzip` 与 `SaveSystem.Zip` 改写，
  容器格式因此在构造上保持一致。写盘前会把压缩后的字节重新解包并解析；若字段不为 `false`
  或 JSON 无法解析，则放弃写入。
- 该标记属于进程级状态，每次启动均为 `false`。游戏每次存档都会把当前值写入存档，
  因此只有清除文件中保存的值，改动才会持久。

## 构建

依赖：Windows 自带的 .NET Framework 4.x 编译器，无需 SDK、NuGet 包或网络。

```powershell
pwsh -File build.ps1 -GameDir 'X:\Steam\steamapps\common\Casualties Unknown'
```

`build.ps1` 会从仓库内或与仓库同级的 `Casualties Unknown` 目录解析游戏路径，可用 `-GameDir` 覆盖。
加 `-Deploy` 会把编译产物复制到游戏的 `BepInEx\plugins` 目录。
