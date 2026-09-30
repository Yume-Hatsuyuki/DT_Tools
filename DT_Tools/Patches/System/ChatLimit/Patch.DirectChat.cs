using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// UI_DirectChat.Init 后置：抬高私聊输入框上限。
    /// 公共方法（nameof 定位），0.1.15b UI_DirectChat.cs:67（:88 写 characterLimit = 100）；
    /// 私有字段 _input：0.1.15b UI_DirectChat.cs:26。
    /// </summary>
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
