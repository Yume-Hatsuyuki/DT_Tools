using BepInEx.Configuration;
using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// LOBBY_MIN_PLAYER：正式服返回 Value（默认 5）；Playtest 仍为 0。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "LOBBY_MIN_PLAYER",
        description: "房间开局最少人数：正式服可改（默认 5）；测试模式开启时仍为 0。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class LobbyMinPlayersFeature
    {
        [ConfigField(5, "正式服开局所需最少人数，游戏默认为 5。")]
        public static ConfigEntry<int> Value;

        [HarmonyPatch(typeof(Define), nameof(Define.LOBBY_MIN_PLAYER), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool Prefix(ref int __result)
        {
            __result = Define.IsPlaytestApp ? 0 : Value.Value;
            return false;
        }
    }
}
