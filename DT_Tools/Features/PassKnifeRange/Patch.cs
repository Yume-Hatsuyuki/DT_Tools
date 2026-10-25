using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.PassKnifeRange
{
    [HarmonyPatch(typeof(MyPlayer), "GetHandWeaponTarget")]
    internal static class PassKnifeRangePatch
    {
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            if (!Engine.Enabled<PassKnifeRangeFeature>())
                return true;

            __result = NearestTargetFinder.Find(
                __instance.Position,
                PassKnifeRangeFeature.Range,
                player => !Managers.Player.KnownBlackIds.Contains(player.PublicInfo.PlayerId));
            return false;
        }
    }
}
