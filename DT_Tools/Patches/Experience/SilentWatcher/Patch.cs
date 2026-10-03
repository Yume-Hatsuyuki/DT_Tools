using HarmonyLib;

namespace DT_Tools.Patches.Experience.SilentWatcher
{
    /// <summary>
    /// 监听通道与 KeywordTaunt 相同（惰性订阅 OnChatReceived），扫描逻辑在 SilentWatcherLogic
    /// （普通类，规避 Harmony 分析器对 struct 参数的误报），协程经 CoroutineHost 常驻运行。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.ProcessChatMessages))]
    internal static class SilentWatcherPatch
    {
        private static void Prefix(VoiceManager __instance)
        {
            SilentWatcherLogic.EnsureSubscribed(__instance);
        }
    }
}
