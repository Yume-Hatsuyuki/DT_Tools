using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// UI_ChatDevicePopup.Init 后置：抬高聊天设备输入框上限。
    /// 公共方法（nameof 定位），0.1.15b UI_ChatDevicePopup.cs:74（:95 写 characterLimit = 100）；
    /// 私有字段 _input：0.1.15b UI_ChatDevicePopup.cs:42。
    /// </summary>
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
