using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatLimit
{
    [HarmonyPatch(typeof(UI_TrialChat), nameof(UI_TrialChat.Init))]
    internal static class ChatLimitTrialChatPatch
    {
        private static void Postfix(UI_TrialChat __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            ChatLimitLogic.RaiseLimit(
                Traverse.Create(__instance).Field("_input").GetValue<TMP_InputField>());
        }
    }

    // 0.1.21h 的聊天输入框额外用 Fits 限制语言字数与 2 行行数，需一并放行。
    [HarmonyPatch(typeof(UI_TrialChat), "Fits")]
    internal static class ChatLimitFitsPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}
