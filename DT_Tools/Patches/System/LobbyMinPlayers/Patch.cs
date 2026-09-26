using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.LobbyMinPlayers
{
    /// <summary>
    /// LOBBY_MIN_PLAYER getter 整替：0.1.15b Define.cs:2031-2042（正式服 5 / Playtest 0）。
    /// Playtest 分支不再复制原版判断，直接放行原版 getter 自算（Define.cs:2035 调 IsPlaytestApp），
    /// 语义随游戏版本自动跟随。
    /// </summary>
    [HarmonyPatch(typeof(Define), nameof(Define.LOBBY_MIN_PLAYER), MethodType.Getter)]
    internal static class LobbyMinPlayersPatch
    {
        private static bool Prefix(ref int __result)
        {
            if (!Engine.Enabled<LobbyMinPlayersFeature>())
                return true;

            // Playtest 构建放行原版自算（原版返回 0）。注意 Dev/PlaytestMode 开启时 IsPlaytestApp
            // getter 已被整替为恒 true（Patch.PlaytestApp.cs，Define.cs:2013），此处读到的即补丁后
            // 的值 → 同样放行走原版 0，属预期组合：PlaytestMode 语义优先于本功能的固定下限。
            if (Define.IsPlaytestApp)
                return true;

            __result = LobbyMinPlayersFeature.Value;
            return false;
        }
    }
}
