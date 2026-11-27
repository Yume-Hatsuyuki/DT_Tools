// 作用：为运动残影挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.MotionAfterimage
{
    [HarmonyPatch(typeof(UI_GameScene), "LateUpdate")]
    internal static class MotionAfterimageTickPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<MotionAfterimageFeature>())
                return;

            MotionAfterimageLogic.Tick();
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.Despawn), new[] { typeof(int) })]
    internal static class MotionAfterimageDespawnPatch
    {
        private static void Postfix(int playerId)
        {
            if (!Engine.Enabled<MotionAfterimageFeature>())
                return;

            MotionAfterimageLogic.OnPlayerDespawned(playerId);
        }
    }

    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
    internal static class MotionAfterimageLocalDeadPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<MotionAfterimageFeature>())
                return;

            MotionAfterimageLogic.OnLocalPlayerDead();
        }
    }
}
