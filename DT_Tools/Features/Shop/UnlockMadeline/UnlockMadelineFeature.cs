using global::System.Collections.Generic;
using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    /// <summary>解锁 Madeline(101) 并补齐缺失资源/UI。</summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "IncludeMadeline",
        description: "梅德琳解锁：解锁梅德琳并修复缺失资源。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static partial class UnlockMadelineFeature
    {
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsCharacterOwned(int dataId, ref bool __result)
        {
            if (dataId != 101)
                return true;
            __result = true;
            return false;
        }

        // Postfix 可与 OwnedCharacterIds 全解锁 Prefix 叠加
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds),
            MethodType.Getter)]
        [HarmonyPostfix]
        private static void PostfixOwnedCharacterIds(ref IReadOnlyList<int> __result)
        {
            if (__result == null)
                return;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] == 101)
                    return;
            }
            var list = new List<int>(__result.Count + 1);
            list.AddRange(__result);
            list.Add(101);
            __result = list;
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.IsCharacterOwned))]
        [HarmonyPrefix]
        private static bool PrefixSaveManagerIsCharacterOwned(int dataId, ref bool __result)
        {
            if (dataId != 101)
                return true;
            __result = true;
            return false;
        }
    }
}
