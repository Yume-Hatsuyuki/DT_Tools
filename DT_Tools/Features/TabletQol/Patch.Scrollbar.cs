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

            if (TabletQolFeature.ScrollbarClue)
                TabletQolScrollbar.Attach(tablet.ClueSrollRect, "线索", reason);
        }
    }

    // 0.1.21h 起平板聊天页移除，聊天列表统一由 UI_TrialChat 承载。
    [HarmonyPatch(typeof(UI_TrialChat), nameof(UI_TrialChat.Init))]
    internal static class PatchTabletQolChatScrollbar
    {
        [HarmonyPostfix]
        private static void Postfix(UI_TrialChat __instance)
        {
            if (!Engine.Enabled<TabletQolFeature>()
                || !TabletQolFeature.Scrollbar
                || !TabletQolFeature.ScrollbarChat)
                return;

            ScrollRect chat = Traverse.Create(__instance).Field("_scroll").GetValue<ScrollRect>();
            TabletQolScrollbar.Attach(chat, "聊天", "聊天窗口");
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
