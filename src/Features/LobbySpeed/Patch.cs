// 作用：为大厅移速调整挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.LobbySpeed
{
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
