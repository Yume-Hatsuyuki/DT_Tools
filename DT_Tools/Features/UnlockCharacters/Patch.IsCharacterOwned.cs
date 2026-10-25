using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockCharacters
{
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
    internal static class UnlockCharactersIsCharacterOwnedPatch
    {
        private static bool Prefix(int dataId, ref bool __result)
        {
            if (!Engine.Enabled<UnlockCharactersFeature>())
                return true;

            if (dataId == 101)
                return true;

            __result = true;
            return false;
        }
    }
}
