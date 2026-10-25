using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatLimit
{
    [HarmonyPatch(typeof(UI_ChatDevicePopup), nameof(UI_ChatDevicePopup.Init))]
    internal static class ChatLimitChatDevicePatch
    {
        private static void Postfix(UI_ChatDevicePopup __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            ChatLimitLogic.RaiseLimit(
                Traverse.Create(__instance).Field("_input").GetValue<TMP_InputField>());
        }
    }
}
