using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatLimit
{
    [HarmonyPatch(typeof(UI_DirectChat), nameof(UI_DirectChat.Init))]
    internal static class ChatLimitDirectChatPatch
    {
        private static void Postfix(UI_DirectChat __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            ChatLimitLogic.RaiseLimit(
                Traverse.Create(__instance).Field("_input").GetValue<TMP_InputField>());
        }
    }
}
