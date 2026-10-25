using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockCharacters
{
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockCharactersStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockCharactersFeature>();
    }
}
