using BepInEx.Configuration;
using HarmonyLib;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Int32 Define::get_LOBBY_MIN_PLAYER()
    /// 房间开局最少人数（保留原版 IsPlaytestApp 分支）
    /// 
    /// 原逻辑：
    ///   if (!IsPlaytestApp) return 5;
    ///   return 0;
    /// 
    /// 补丁逻辑（启用后）：
    ///   if (!IsPlaytestApp) return [LobbyMinPlayer.MinPlayer];  // 默认 5（与原版一致）
    ///   return 0;                                               // Playtest 路径保持原版
    /// 
    /// 与「测试模式」配合：测试模式强制 IsPlaytestApp=true 时，这里会自然走到 return 0，
    /// </summary>
    [HarmonyPatch]
    [PatchConfig("Enable_Patch_LobbyMinPlayer", "房间开局最少人数：从 .cfg [LobbyMinPlayer] 读取 MinPlayer（默认 5）。\n仅覆盖正式服路径，PlaytestApp 路径仍返回 0。", author: "梦初雪")]
    internal static class Patch_LobbyMinPlayer
    {
        private static ConfigEntry<int> _minPlayer;

        static Patch_LobbyMinPlayer()
        {
            var cfg = Plugin.Instance.Config;
            _minPlayer = cfg.Bind(
                "LobbyMinPlayer",
                "MinPlayer",
                5,
                new ConfigDescription("正式服开局所需最少人数。游戏原版为 5。PlaytestApp 路径不受影响（仍为 0）。")
            );
        }

        [HarmonyPatch(typeof(Define), nameof(Define.LOBBY_MIN_PLAYER), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixMinPlayer(ref int __result)
        {
            // 严格保留原版分支结构，只替换正式服一侧的返回值
            if (!Define.IsPlaytestApp)
            {
                __result = _minPlayer.Value;
                return false;
            }

            // Playtest 路径：与原版一致
            __result = 0;
            return false;
        }
    }
}