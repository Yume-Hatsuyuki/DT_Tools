using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// UI_GameTablet.Init 后置：抬高平板聊天输入框上限。
    /// 公共方法（nameof 定位），0.1.15b UI_GameTablet.cs:821（:871 写 characterLimit = 100）。
    /// GetInputField 为 UI_Base 的 protected 方法（0.1.15b UI_Base.cs:98），Traverse 定位。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.Init))]
    internal static class ChatLimitTabletPatch
    {
        private static void Postfix(UI_GameTablet __instance)
        {
            if (!Engine.Enabled<ChatLimitFeature>())
                return;

            try
            {
                var field = Traverse.Create(__instance)
                    .Method("GetInputField", new object[] { 0 }).GetValue<TMP_InputField>();
                ChatLimitLogic.RaiseLimit(field);
            }
            catch (global::System.Exception ex)
            {
                // UI 未就绪等异常记诊断，等 TMP OnEnable / ActivateInputField 兜底
                Log.Debug<ChatLimitFeature>($"平板输入框抬限失败: {ex.Message}");
            }
        }
    }
}
