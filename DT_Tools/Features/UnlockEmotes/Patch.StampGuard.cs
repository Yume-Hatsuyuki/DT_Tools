using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockEmotes
{
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockEmotesStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockEmotesFeature>();
    }
}
