using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatHistory
{
    [HarmonyPatch(typeof(UI_DirectChat), nameof(UI_DirectChat.Init))]
    internal static class ChatHistoryMountDirectChatPatch
    {
        private static void Postfix(UI_DirectChat __instance, bool __result)
        {
            if (!__result || !Engine.Enabled<ChatHistoryFeature>())
                return;
            var input = Traverse.Create(__instance).Field<TMP_InputField>("_input").Value;
            if (input != null)
                Util.GetOrAddComponent<ChatHistoryNavigator>(input.gameObject)
                    .Bind(input, ChatHistorySource.ChatBar);
        }
    }

    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.Init))]
    internal static class ChatHistoryMountTabletPatch
    {
        private static void Postfix(UI_GameTablet __instance, bool __result)
        {
            if (!__result || !Engine.Enabled<ChatHistoryFeature>())
                return;
            var input = Util.FindChild<TMP_InputField>(__instance.gameObject, "ChatInputField", recursive: true);
            if (input != null)
                Util.GetOrAddComponent<ChatHistoryNavigator>(input.gameObject)
                    .Bind(input, ChatHistorySource.Tablet);
        }
    }

    [HarmonyPatch(typeof(UI_ChatDevicePopup), nameof(UI_ChatDevicePopup.Init))]
    internal static class ChatHistoryMountInterphonePatch
    {
        private static void Postfix(UI_ChatDevicePopup __instance, bool __result)
        {
            if (!__result || !Engine.Enabled<ChatHistoryFeature>())
                return;
            var input = Traverse.Create(__instance).Field<TMP_InputField>("_input").Value;
            if (input == null)
                return;
            var navigator = Util.GetOrAddComponent<ChatHistoryNavigator>(input.gameObject);
            navigator.Bind(input, ChatHistorySource.Interphone);
            var secret = Traverse.Create(__instance).Field<bool>("_isSecret").Value;
            navigator.Channel = secret
                ? ChatHistoryChannel.InterphoneSecret
                : ChatHistoryChannel.InterphoneOpen;
        }
    }

    [HarmonyPatch(typeof(UI_ChatDevicePopup), nameof(UI_ChatDevicePopup.Open))]
    internal static class ChatHistoryInterphoneChannelPatch
    {
        private static void Postfix(UI_ChatDevicePopup __instance, bool isSecret)
        {
            if (!Engine.Enabled<ChatHistoryFeature>())
                return;
            var input = Traverse.Create(__instance).Field<TMP_InputField>("_input").Value;
            var navigator = input == null ? null : input.GetComponent<ChatHistoryNavigator>();
            if (navigator != null)
                navigator.Channel = isSecret
                    ? ChatHistoryChannel.InterphoneSecret
                    : ChatHistoryChannel.InterphoneOpen;
        }
    }
}
