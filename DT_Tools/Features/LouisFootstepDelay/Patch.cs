using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.LouisFootstepDelay
{
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class LouisFootstepDelayPatch
    {
        private static void Postfix(Server.Game.Player __instance, Server.Game.Player black)
        {
            if (!Engine.Enabled<LouisFootstepDelayFeature>())
                return;
            try
            {
                if (__instance == null || black == null || !black.IsAlive)
                    return;
                if (__instance.BuffComponent.HasBuff(EBuffType.DeadDetective))
                {
                    black.BuffComponent.AddBuff(EBuffType.Footprint, LouisFootstepDelayFeature.FootprintMs, isBroadcast: false);
                }
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<LouisFootstepDelayFeature>(ex, "路易斯脚印延长失败（可忽略）");
            }
        }
    }
}
