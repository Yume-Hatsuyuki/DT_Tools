using System.Collections.Generic;
using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    /// <summary>
    /// 角色全解锁（不含 101 Madeline）。IsCharacterOwned 对非 101 恒 true。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "OwnedCharacterIds",
        description: "角色全解锁：本地视为拥有全部可选角色（不含梅德琳）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class UnlockCharactersFeature
    {
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsCharacterOwned(int dataId, ref bool __result)
        {
            if (dataId == 101)
                return true;

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds),
            MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixOwnedCharacterIds(ref IReadOnlyList<int> __result)
        {
            var list = new List<int>(Define.DEFAULT_OWNED_CHARACTER_IDS);
            foreach (int id in Define.SHOP_CHARACTER_IDS)
                list.Add(id);
            __result = list;
            return false;
        }
    }
}
