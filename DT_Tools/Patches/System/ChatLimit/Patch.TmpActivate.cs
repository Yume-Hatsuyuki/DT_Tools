using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// TMP_InputField.ActivateInputField 后置：输入框激活时再兜底抬限。
    /// 公共方法（nameof 定位）；Unity TextMeshPro 程序集成员（不在 0.1.15b 反编译源内，
    /// 已核对 libs/Unity.TextMeshPro.dll 元数据含 ActivateInputField）。
    /// </summary>
    [HarmonyPatch(typeof(TMP_InputField), nameof(TMP_InputField.ActivateInputField))]
    internal static class ChatLimitTmpActivatePatch
    {
        private static void Postfix(TMP_InputField __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            ChatLimitLogic.RaiseLimit(__instance);
        }
    }
}
