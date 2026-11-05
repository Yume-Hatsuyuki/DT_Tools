using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatHistory
{
    [HarmonyPatch(typeof(UI_TrialChat), nameof(UI_TrialChat.Init))]
    internal static class ChatHistoryMountTrialChatPatch
    {
        private static void Postfix(UI_TrialChat __instance, bool __result)
        {
            if (!__result || !Engine.Enabled<ChatHistoryFeature>())
                return;
            var input = Traverse.Create(__instance).Field<TMP_InputField>("_input").Value;
            if (input == null)
                return;
            // 0.1.21h 起大厅与庭审共用 UI_TrialChat：大厅实例挂在 UI_GameScene 下，
            // 庭审实例挂在 UI_TrialEvent 下，按层级还原「聊天条 / 平板」两个配置项。
            var source = __instance.GetComponentInParent<UI_TrialEvent>() != null
                ? ChatHistorySource.Tablet
                : ChatHistorySource.ChatBar;
            Util.GetOrAddComponent<ChatHistoryNavigator>(input.gameObject).Bind(input, source);
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
