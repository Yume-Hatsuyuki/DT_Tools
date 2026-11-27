// 作用：为取消聊天输入框字数截断挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatLimit
{
    [HarmonyPatch(typeof(TMP_InputField), "OnEnable")]
    internal static class ChatLimitTmpOnEnablePatch
    {
        private static void Postfix(TMP_InputField __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            ChatLimitLogic.RaiseLimit(__instance);
        }
    }
}
