using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.LobbySpeed
{
    /// FixedUpdateMove 前置改写 deltaSpeed（方法为 private，字符串定位：
    /// 0.1.16b MyPlayer.cs:1869）。仅 Lobby 生效，其余阶段原样放行。
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateMove")]
    internal static class LobbySpeedPatch
    {
        private static void Prefix(ref float deltaSpeed)
        {
            if (!Engine.Enabled<LobbySpeedFeature>())
                return;

            if (Managers.Game.State == EGameState.Lobby)
                deltaSpeed = LobbySpeedFeature.LobbyDeltaSpeed;
        }
    }
}
