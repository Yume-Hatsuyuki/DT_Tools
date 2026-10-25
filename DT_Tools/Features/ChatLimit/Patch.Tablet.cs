using DT_Tools.Core;
using HarmonyLib;
using TMPro;

namespace DT_Tools.Features.ChatLimit
{
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
                Log.Debug<ChatLimitFeature>($"平板输入框抬限失败: {ex.Message}");
            }
        }
    }
}
