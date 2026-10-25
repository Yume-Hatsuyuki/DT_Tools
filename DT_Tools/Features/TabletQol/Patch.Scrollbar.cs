using DT_Tools.Core;
using HarmonyLib;
using UnityEngine.UI;

namespace DT_Tools.Features.TabletQol
{
    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.Init))]
    internal static class PatchTabletQolScrollbar
    {
        [HarmonyPostfix]
        private static void Postfix(UI_GameTablet __instance)
        {
            if (!Engine.Enabled<TabletQolFeature>())
                return;

            AttachConfigured(__instance, "Init");
        }

        internal static void AttachConfigured(UI_GameTablet tablet, string reason)
        {
            if (tablet == null || !TabletQolFeature.Scrollbar)
                return;

            if (TabletQolFeature.ScrollbarChat)
            {
                ScrollRect chat = Traverse.Create(tablet).Field("_chatScroll").GetValue<ScrollRect>();
                TabletQolScrollbar.Attach(chat, "聊天", reason);
            }

            if (TabletQolFeature.ScrollbarClue)
                TabletQolScrollbar.Attach(tablet.ClueSrollRect, "线索", reason);
        }
    }

    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.Section), MethodType.Setter)]
    internal static class PatchTabletQolScrollbarOnSection
    {
        [HarmonyPostfix]
        private static void Postfix(UI_GameTablet __instance)
        {
            if (!Engine.Enabled<TabletQolFeature>())
                return;

            PatchTabletQolScrollbar.AttachConfigured(__instance, "切换页签");
        }
    }
}
