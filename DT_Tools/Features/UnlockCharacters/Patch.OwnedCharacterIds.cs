using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockCharacters
{
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
