using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DarkRadar
{
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    internal static class DarkRadarPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<DarkRadarFeature>())
                return;

            DarkRadarLogic.SyncAll();
        }
    }
}
