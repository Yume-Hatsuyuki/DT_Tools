using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.WebTablet
{
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.State), MethodType.Setter)]
    internal static class WebTabletStatePatch
    {
        private static void Postfix(EGameState value)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordStateChange(value);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "阶段记录失败");
            }
        }
    }
}
