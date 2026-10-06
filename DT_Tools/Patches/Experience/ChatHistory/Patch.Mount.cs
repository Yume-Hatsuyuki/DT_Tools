using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.Experience.ChatHistory
{
    /// <summary>
    /// 挂载点一：UI_DirectChat.Init。覆盖聊天快捷条全部实例——大厅（Lobby 场景预制体挂载
    /// UI_DirectChat_Lobby）、对局昼夜（0.1.16b UI_GameScene.cs:577）与庭审
    /// （0.1.16b UI_TrialEvent.cs:392）；子类不重写 Init，基类方法每次实例化都会命中。
    /// _input 为私有字段（0.1.16b UI_DirectChat.cs:26），按 AGENTS.md §10 反射定位。
    /// </summary>
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

    /// <summary>
    /// 挂载点二：UI_GameTablet.Init 的庭审平板聊天输入框（ChatSection 的 ChatInputField，
    /// 绑定与提交锚点 0.1.16b UI_GameTablet.cs:829/:869）。按枚举名直接查找子对象，
    /// 与 BindInputField 的取数路径同源（Util.FindChild，0.1.16b Util.cs:302），
    /// 不走 GetInputField（UI_Base.cs:98 protected）的方法反射绑定。平板与快捷条
    /// 同为全局聊天频道，共用历史。
    /// </summary>
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

    /// <summary>
    /// 挂载点三：UI_ChatDevicePopup.Init 的打字机（对讲机）输入框（ChatField，
    /// 私有字段 _input 0.1.16b UI_ChatDevicePopup.cs:42）。频道按 Init 时的 _isSecret
    /// （:36）设定初值，之后由 Open 补丁动态修正；兜底扫描亦会按当前模式同步。
    /// </summary>
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

    /// <summary>
    /// 打字机频道修正：UI_ChatDevicePopup.Open(device, isSecret, readOnly) 每次打开决定
    /// 公开/秘密模式（0.1.16b UI_ChatDevicePopup.cs:143，_isSecret 私有字段 :36）。
    /// Postfix 按实参切换导航器历史频道；readOnly 时输入框被隐藏（OnReadOnlyChanged），
    /// 导航器无焦点不响应，无需特判。
    /// </summary>
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
