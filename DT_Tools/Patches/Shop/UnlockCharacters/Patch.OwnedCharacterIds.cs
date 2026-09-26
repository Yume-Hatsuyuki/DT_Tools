using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockCharacters
{
    /// <summary>
    /// 原版 = 默认角色 + 已确认购买的商店角色（属性 getter，public，可用 nameof）：
    /// 0.1.15b SteamInventorySource.cs:145。整替为默认 + 全部商店角色（不含 101）。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds),
        MethodType.Getter)]
    internal static class UnlockCharactersOwnedCharacterIdsPatch
    {
        private static bool Prefix(ref IReadOnlyList<int> __result)
        {
            if (!Engine.Enabled<UnlockCharactersFeature>())
                return true;

            var list = new List<int>(Define.DEFAULT_OWNED_CHARACTER_IDS);
            foreach (int id in Define.SHOP_CHARACTER_IDS)
                list.Add(id);

            __result = list;
            return false;
        }
    }
}
