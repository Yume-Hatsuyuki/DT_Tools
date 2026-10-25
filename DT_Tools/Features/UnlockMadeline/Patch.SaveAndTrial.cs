using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.UnlockMadeline
{
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
