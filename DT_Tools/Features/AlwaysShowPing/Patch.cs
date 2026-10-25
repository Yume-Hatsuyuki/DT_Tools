using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.AlwaysShowPing
{
    [HarmonyPatch(typeof(UI_GameScene), "TickPingIndicator")]
    internal static class AlwaysShowPingTickPatch
    {
        private static bool Prefix(UI_GameScene __instance)
        {
            if (!Engine.Enabled<AlwaysShowPingFeature>())
                return true;

            AlwaysShowPingLogic.Tick(__instance);
            return false;
        }
    }
}
