using BepInEx.Configuration;
using HarmonyLib;

namespace DT_Tools.Patches.System
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   Define::get_LOBBY_MIN_PLAYER()
    ///
    /// <b>原版效果</b>：
    ///   if (!IsPlaytestApp) return 5; else return 0;
    ///
    /// <b>修改后效果</b>：
    ///   正式服路径返回 [LOBBY_MIN_PLAYER].Value（默认 5）；
    ///   Playtest 路径仍返回 0。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 重写 Getter，保留原版分支结构。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "Enable_LOBBY_MIN_PLAYER",
        "房间开局最少人数：可在下方配置段修改（默认 5）。测试模式开启时仍为 0。",
        author: "梦初雪")]
    internal static class Patch_LOBBY_MIN_PLAYER
    {
        private static ConfigEntry<int> _value;

        static Patch_LOBBY_MIN_PLAYER()
        {
            _value = Plugin.Instance.Config.Bind(
                "LOBBY_MIN_PLAYER",
                "Value",
                5,
                new ConfigDescription("正式服开局所需最少人数，游戏默认为 5。测试模式开启时本项无效。"));
        }

        [HarmonyPatch(typeof(Define), nameof(Define.LOBBY_MIN_PLAYER), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool Prefix(ref int __result)
        {
            if (!Define.IsPlaytestApp)
            {
                __result = _value.Value;
                return false;
            }

            __result = 0;
            return false;
        }
    }
}
