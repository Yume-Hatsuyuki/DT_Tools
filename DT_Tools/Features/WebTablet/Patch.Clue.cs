using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// UI_GameTablet.AddClue 后缀（public，可用 nameof）：0.1.18c UI_GameTablet.cs:1694。
    /// 自己收集线索时由 ClueManager.AddClue 调用（ownerId 为本机）；学级裁判中提交命题时由
    /// UI_TrialEvent.CompleteProposition 调用（0.1.18c UI_TrialEvent.cs:1014，isProposition 为 true）。
    /// </summary>
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

    /// <summary>
    /// UI_GameTablet.AddPropositionMessage 后缀（public，可用 nameof）：0.1.18c UI_GameTablet.cs:2515。
    /// 命题在聊天流中显示为命题气泡，由 UI_TrialEvent.CompleteProposition 调用（0.1.18c UI_TrialEvent.cs:1023）。
    /// </summary>
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
