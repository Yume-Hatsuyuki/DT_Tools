using HarmonyLib;

namespace DT_Tools.Patches.Experience.TalkPolice
{
    /// <summary>
    /// 两路钩子，逻辑均在 TalkPoliceLogic（普通类，规避 Harmony 分析器对 struct 参数的误报）：
    /// 1) ProcessChatMessages Prefix 惰性订阅 OnChatReceived 做窗口计数。
    /// 2) UI_TrialEvent.CountingVote（计票 UI 入口，0.1.16b UI_TrialEvent.cs:864）Postfix：
    ///    每轮计票开始时清空窗口计数，并按 SummaryFormat 播报本场话痨之王。
    /// 点名与播报均走全房广播通道 SendChatMessage。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.ProcessChatMessages))]
    internal static class TalkPolicePatch
    {
        private static void Prefix(VoiceManager __instance)
        {
            TalkPoliceLogic.EnsureSubscribed(__instance);
        }
    }

    /// <summary>计票时清空窗口并播报话痨之王（独立补丁类）。</summary>
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.CountingVote))]
    internal static class TalkPoliceVotePatch
    {
        private static void Postfix()
        {
            TalkPoliceLogic.OnCountingVote();
        }
    }
}
