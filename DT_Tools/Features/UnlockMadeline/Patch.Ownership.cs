using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockMadeline
{
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
    internal static class UnlockMadelineSteamOwnedPatch
    {
        private static bool Prefix(int dataId, ref bool __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            if (dataId != UnlockMadelineLogic.MadelineDataId)
                return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds),
        MethodType.Getter)]
    internal static class UnlockMadelineSteamOwnedIdsPatch
    {
        private static void Postfix(ref global::System.Collections.Generic.IReadOnlyList<int> __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (__result == null)
                return;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] == UnlockMadelineLogic.MadelineDataId)
                    return;
            }
            var list = new global::System.Collections.Generic.List<int>(__result.Count + 1);
            list.AddRange(__result);
            list.Add(UnlockMadelineLogic.MadelineDataId);
            __result = list;
        }
    }

}
