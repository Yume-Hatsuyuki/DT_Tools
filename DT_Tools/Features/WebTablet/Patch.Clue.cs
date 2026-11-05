using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.WebTablet
{
    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.AddClue))]
    internal static class WebTabletClueAddPatch
    {
        private static void Postfix(int ownerId, PropositionInfo info, bool isProposition)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordClue(ownerId, info, isProposition);
                // 0.1.21h 移除了 AddPropositionMessage，提交命题只走 AddClue(isProposition:true)。
                if (isProposition)
                    WebTabletBridge.RecordProposition(ownerId, info);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "线索/命题记录失败");
            }
        }
    }
}
