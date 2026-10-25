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
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "线索记录失败");
            }
        }
    }

    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.AddPropositionMessage))]
    internal static class WebTabletPropositionPatch
    {
        private static void Postfix(int playerId, string name, string sentence, PropositionInfo proposition)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordProposition(playerId, name, sentence, proposition);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "命题记录失败");
            }
        }
    }
}
