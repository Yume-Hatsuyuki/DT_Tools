using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockMadeline
{
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
                Log.Exception<UnlockMadelineFeature>(ex, "资源注入失败");
            }
        }
    }
}
