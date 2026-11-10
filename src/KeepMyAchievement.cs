using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KeepMyAchievement
{
    /// <summary>
    /// CU-KeepMyAchievement —— 只做两件事：
    ///   1) 禁用 ConsoleScript.CheckForCheats()，让控制台命令不再置位 SteamManager.disableAchievements
    ///   2) 读档之前把存档 JSON 里的 cheatedSave 改回 false，游戏读到的就是干净数据
    /// 另提供控制台命令 kma（status / unmarksave）用于查状态和手动清理。
    /// </summary>
    [BepInPlugin(Guid, "CU-KeepMyAchievement", Version)]
    public class KeepMyAchievementPlugin : BaseUnityPlugin
    {
        public const string Guid = "sol-long.cu.keepmyachievement";
        public const string Version = "0.1.0";

        private const string CommandName = "kma";
        private const string SubStatus = "status";
        private const string SubUnmark = "unmarksave";

        internal static ManualLogSource Log;

        private static int _patchedMethods = -1;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(CheatPatch));
            _harmony.PatchAll(typeof(CleanSavePatch));
            _harmony.PatchAll(typeof(RegisterCommandPatch));

            try
            {
                int n = 0;
                foreach (var unused in _harmony.GetPatchedMethods()) n++;
                _patchedMethods = n;
            }
            catch
            {
                _patchedMethods = -1;
            }

            Log.LogInfo("CU-KeepMyAchievement v" + Version
                + " loaded. console: kma status | kma unmarksave");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        // ---------------------------------------------------------------- console

        internal static void RegisterConsoleCommand()
        {
            List<Command> commands = ConsoleScript.Commands;
            if (commands == null) return;

            // 场景重载会让 RegisterAllCommands 再跑一次，按名字去重
            for (int i = 0; i < commands.Count; i++)
            {
                Command existing = commands[i];
                if (existing != null && existing.name == CommandName) return;
            }

            Dictionary<int, List<string>> autofill = new Dictionary<int, List<string>>();
            autofill[0] = new List<string> { SubStatus, SubUnmark };

            Command command = new Command(
                CommandName,
                "KeepMyAchievement: \"kma status\" shows state, \"kma unmarksave\" removes the cheated flag from save files",
                new Command.Action(OnCommand),
                autofill,
                new ValueTuple<string, string>[0]);

            commands.Add(command);
            Log.LogInfo("[cmd] registered console command: kma status | kma unmarksave");
        }

        private static void OnCommand(string[] args)
        {
            try
            {
                string sub = (args != null && args.Length > 1 && args[1] != null)
                    ? args[1].Trim().ToLowerInvariant()
                    : string.Empty;

                if (sub == SubStatus) { PrintStatus(); return; }
                if (sub == SubUnmark) { UnmarkSaves(); return; }
                PrintUsage();
            }
            catch (Exception e)
            {
                Log.LogError("[cmd] failed: " + e);
                Say("kma: internal error, see BepInEx log");
            }
        }

        private static void PrintUsage()
        {
            Say("CU-KeepMyAchievement v" + Version);
            Say("  kma status      - show plugin state and each save file's cheated flag");
            Say("  kma unmarksave  - remove the cheated flag from all save files (keeps *.keeper.bak)");
        }

        private static void PrintStatus()
        {
            Say("CU-KeepMyAchievement v" + Version
                + "  |  patched methods: " + (_patchedMethods < 0 ? "unknown" : _patchedMethods.ToString()));
            Say("disableAchievements = " + SteamManager.disableAchievements);
            Say("has save.sv = " + SaveSystem.HasSave());

            List<string> lines = SaveMarker.StatusLines();
            for (int i = 0; i < lines.Count; i++) Say(lines[i]);
        }

        private static void UnmarkSaves()
        {
            Say("CU-KeepMyAchievement: unmarking save files");
            int changed = SaveMarker.CleanAll(Say);
            Say("done: " + changed + " file(s) changed");
        }

        /// <summary>同时写 BepInEx 日志和控制台。仅在控制台命令中使用（Alert 会打开控制台）。</summary>
        internal static void Say(string line)
        {
            Log.LogInfo("[cmd] " + line);
            ConsoleScript console = ConsoleScript.instance;
            if (console) console.Alert(line);
        }

        /// <summary>只写日志的出口，用于读档流程（此时控制台可能是关的）。</summary>
        internal static void LogSink(string line)
        {
            Log.LogInfo("[clean] " + line);
        }
    }

    /// <summary>目标 1：禁用 ConsoleScript.CheckForCheats()，原方法体不执行。</summary>
    [HarmonyPatch(typeof(ConsoleScript), "CheckForCheats")]
    internal static class CheatPatch
    {
        private static bool Prefix()
        {
            KeepMyAchievementPlugin.Log.LogInfo("[patch] CheckForCheats skipped");
            return false;
        }
    }

    /// <summary>目标 2：读档之前清掉存档里的 cheatedSave 标记。</summary>
    [HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
    internal static class CleanSavePatch
    {
        private static void Prefix()
        {
            int changed = SaveMarker.CleanAll(KeepMyAchievementPlugin.LogSink);
            if (changed > 0)
                KeepMyAchievementPlugin.Log.LogInfo("[patch] cleared the cheated flag in " + changed + " file(s) before loading");
        }
    }

    /// <summary>把 kma 注册进游戏控制台。</summary>
    [HarmonyPatch(typeof(ConsoleScript), "RegisterAllCommands")]
    internal static class RegisterCommandPatch
    {
        private static void Postfix()
        {
            KeepMyAchievementPlugin.RegisterConsoleCommand();
        }
    }

    /// <summary>存档标记的读与写，压缩/解压一律走游戏自己的 SaveSystem.Unzip / Zip。</summary>
    internal static class SaveMarker
    {
        internal const string BackupSuffix = ".keeper.bak";

        private static string SaveDir
        {
            get { return Application.persistentDataPath; }
        }

        internal static string[] SaveFiles()
        {
            List<string> files = new List<string>();
            try
            {
                string[] all = Directory.GetFiles(SaveDir);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].EndsWith(".sv", StringComparison.OrdinalIgnoreCase)) files.Add(all[i]);
                }
            }
            catch (Exception e)
            {
                KeepMyAchievementPlugin.Log.LogError("[clean] cannot scan save directory: " + e.Message);
            }
            return files.ToArray();
        }

        private static bool TryReadFlag(string path, out bool flag, out bool hasField)
        {
            flag = false;
            hasField = false;
            try
            {
                string json = SaveSystem.Unzip(File.ReadAllBytes(path));
                JToken token = JObject.Parse(json)["cheatedSave"];
                if (token == null) return true;
                hasField = true;
                if (token.Type == JTokenType.Boolean) flag = token.Value<bool>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static List<string> StatusLines()
        {
            List<string> lines = new List<string>();
            string[] files = SaveFiles();
            if (files.Length == 0)
            {
                lines.Add("save files: none found in " + SaveDir);
                return lines;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                bool flag;
                bool hasField;
                if (!TryReadFlag(files[i], out flag, out hasField)) lines.Add(name + ": unreadable");
                else if (!hasField) lines.Add(name + ": no cheatedSave field");
                else lines.Add(name + ": cheatedSave=" + (flag ? "true" : "false"));
            }
            return lines;
        }

        internal static int CleanAll(Action<string> say)
        {
            string[] files = SaveFiles();
            if (files.Length == 0)
            {
                say("no save files found in " + SaveDir);
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string name = Path.GetFileName(path);

                bool flag;
                bool hasField;
                if (!TryReadFlag(path, out flag, out hasField))
                {
                    say(name + ": unreadable, skipped");
                    continue;
                }
                if (!hasField)
                {
                    say(name + ": no cheatedSave field, skipped");
                    continue;
                }
                if (!flag)
                {
                    say(name + ": already clean");
                    continue;
                }

                try
                {
                    if (Rewrite(path))
                    {
                        changed++;
                        say(name + ": true -> false (backup " + name + BackupSuffix + ")");
                    }
                }
                catch (Exception e)
                {
                    say(name + ": failed - " + e.Message);
                }
            }
            return changed;
        }

        private static bool Rewrite(string path)
        {
            string json = SaveSystem.Unzip(File.ReadAllBytes(path));
            JObject obj = JObject.Parse(json);
            obj["cheatedSave"] = false;
            byte[] packed = SaveSystem.Zip(obj.ToString(Formatting.None));

            // 写盘前用游戏自己的管线回读校验，确认能解压、能解析、字段确实为 false
            JObject verify = JObject.Parse(SaveSystem.Unzip(packed));
            JToken token = verify["cheatedSave"];
            if (token == null || token.Type != JTokenType.Boolean || token.Value<bool>())
                throw new InvalidOperationException("read-back verification failed, file not written");

            string backup = path + BackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup);
            File.WriteAllBytes(path, packed);
            return true;
        }
    }
}
