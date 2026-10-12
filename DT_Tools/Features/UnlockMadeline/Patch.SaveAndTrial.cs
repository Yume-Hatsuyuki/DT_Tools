using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.UnlockMadeline
{
    /// SaveManager.EnsureCharacterStats 后缀（public，可用 nameof）：
    /// 0.1.16b SaveManager.cs:448（待核对）。原版补齐 CharacterPlayCounts 时明确跳过 101
    /// （SaveManager.cs:454），此处为 101 补 0 并 MarkDirty。
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.EnsureCharacterStats))]
    internal static class UnlockMadelineEnsureStatsPatch
    {
        private static void Postfix(SaveManager __instance)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            UnlockMadelineLogic.SeedPlayCount(__instance);
        }
    }

    /// SaveManager.RecordGamePlayed 前缀（public，可用 nameof）：
    /// 0.1.16b SaveManager.cs:466（待核对）。原版只对已有键自增（SaveManager.cs:470），
    /// 先为 101 补键，使本局对战计入了梅德琳的游玩次数。
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.RecordGamePlayed))]
    internal static class UnlockMadelineRecordPlayedPatch
    {
        private static void Prefix(SaveManager __instance, int characterId)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (characterId != UnlockMadelineLogic.MadelineDataId)
                return;

            UnlockMadelineLogic.SeedPlayCount(__instance);
        }
    }

    /// UI_InfomationPopup.RefreshProfile 前缀（私有方法，字符串定位）：
    /// 0.1.17a UI_InfomationPopup.cs:381。其立绘位置表 STANDING_POS_LIST
    /// （私有静态只读字典：UI_InfomationPopup.cs:68）缺 Madeline/Medelin 键，
    /// 原版回退通用底位；此处预先补键（void 前缀，不拦截原方法）。
    [HarmonyPatch(typeof(UI_InfomationPopup), "RefreshProfile")]
    internal static class UnlockMadelineProfilePosPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            UnlockMadelineLogic.EnsureProfileStandingPos();
        }
    }

    /// UI_TrialEvent.ShowCutscene 后缀（public，可用 nameof）：
    /// 0.1.16b UI_TrialEvent.cs:1081（待核对）。Madeline 的过场表情图未随资源表加载，
    /// 此处在过场显示时从表情图集随机取一张替换表情层（绑定器索引 7 / 9）。
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.ShowCutscene))]
    internal static class UnlockMadelineCutsceneExpPatch
    {
        private static void Postfix(UI_TrialEvent __instance, string characterName)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (characterName != "Madeline" && characterName != "Medelin")
                return;

            Sprite pick = UnlockMadelineLogic.PickRandomExp();
            if (pick == null)
                return;

            UnlockMadelineUi.SetExpLayer(__instance, 7, pick);
            UnlockMadelineUi.SetExpLayer(__instance, 9, pick);
        }
    }
}
