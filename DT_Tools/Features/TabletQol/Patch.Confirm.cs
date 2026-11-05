using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.TabletQol
{
    // 0.1.21h 起提交入口从 UI_ClueSubItem 的按钮移到庭审界面：OnClickSubmit -> SubmitFocusedClue。
    [HarmonyPatch(typeof(UI_TrialScreenPopup), "SubmitFocusedClue")]
    internal static class PatchTabletQolConfirmSubmit
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_TrialScreenPopup __instance)
        {
            if (!Engine.Enabled<TabletQolFeature>())
                return true;
            if (!TabletQolFeature.ConfirmSubmit)
                return true;
            if (TabletQolConfirm.Suppress)
                return true;
            if (!UI_ClueSubItem.CanSubmitProposition())
                return true;

            UI_ClueSubItem card = FindFocusedCard(__instance);
            TabletQolConfirm.Show(card, __instance.transform,
                () => TabletQolConfirm.Submit(__instance));
            return false;
        }

        private static UI_ClueSubItem FindFocusedCard(UI_TrialScreenPopup popup)
        {
            PropositionInfo focus = Traverse.Create(popup).Field("_focusClue").GetValue<PropositionInfo>();
            List<UI_ClueSubItem> cells = Traverse.Create(popup).Field("_clueCells").GetValue<List<UI_ClueSubItem>>();
            if (focus == null || cells == null)
                return null;

            foreach (UI_ClueSubItem cell in cells)
            {
                PropositionInfo info = cell != null ? cell.Clue?.Info : null;
                if (info != null && info.PlayerId == focus.PlayerId
                    && info.Time == focus.Time && info.RoomId == focus.RoomId)
                    return cell;
            }
            return null;
        }
    }
}
