using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// TMP_InputField.OnEnable 后置：任意输入框启用时，若仍是原版 100 上限则抬高。
    /// 目标为 Unity TextMeshPro 程序集成员（不在 0.1.15b 反编译源内）；
    /// protected override，字符串定位，已核对 libs/Unity.TextMeshPro.dll 元数据含 OnEnable。
    /// </summary>
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
