using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.MotionAfterimage
{
    /// <summary>
    /// 对局 HUD LateUpdate 后置 Tick（0.1.15b UI_GameScene.cs:802）。
    /// </summary>
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

    /// <summary>
    /// 他人 Despawn：只保留最后一次黑色定格（0.1.15b PlayerManager.cs:424）。
    /// Hide/进柜子不走此路径。
    /// </summary>
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

    /// <summary>
    /// 自己死亡：S_DEAD → GameManagerEX.Dead（0.1.15b GameManagerEX.cs:333）。
    /// 原版对自己忽略 S_DESPAWN，故本地定格走此钩子。
    /// </summary>
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
