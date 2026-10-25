using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.TabletQol
{
    [HarmonyPatch(typeof(UI_ClueSubItem), "OnClickPropositionButton")]
    internal static class PatchTabletQolConfirmSubmit
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_ClueSubItem __instance)
        {
            if (!Engine.Enabled<TabletQolFeature>())
                return true;
            if (!TabletQolFeature.ConfirmSubmit)
                return true;
            if (TabletQolConfirm.Suppress)
                return true;
            if (!CanSubmit(__instance))
                return true;

            TabletQolConfirm.Show(__instance);
            return false;
        }

        private static bool CanSubmit(UI_ClueSubItem card)
        {
            try
            {
                return Traverse.Create(card).Method("CanSubmitProposition").GetValue() is bool can && can;
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<TabletQolFeature>(ex, "读 CanSubmitProposition 失败，按原版走（不弹确认栏）");
                return false;
            }
        }
    }
}
