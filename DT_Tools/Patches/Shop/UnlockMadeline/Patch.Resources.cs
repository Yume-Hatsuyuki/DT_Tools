using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// ResourceManager.IsInit 自动属性 setter（public，可用 nameof）：
    /// 0.1.15b ResourceManager.cs:17。资源层初始化完成（置 true）时注入
    /// Madeline 缺失贴图（裁切过程见 Ui.InjectAll）；失败仅记日志不阻断加载。
    /// </summary>
    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.IsInit), MethodType.Setter)]
    internal static class UnlockMadelineResourceInitPatch
    {
        private static void Postfix(ResourceManager __instance, bool value)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (!value)
                return;
            try
            {
                UnlockMadelineUi.InjectAll(__instance);
            }
            catch (global::System.Exception ex)
            {
                Log.Error<UnlockMadelineFeature>("资源注入失败: " + ex);
            }
        }
    }
}
