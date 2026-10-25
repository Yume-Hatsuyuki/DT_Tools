using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
    [HarmonyPatch(typeof(PhotoManager), "ConsumeBudget")]
    internal static class WebTabletPhotoBudgetPatch
    {
        private static void Postfix(PhotoManager __instance)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordPhotoBudget(__instance);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "照片冷却记录失败");
            }
        }
    }
}
