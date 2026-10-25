using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.RemoveWallCollision
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.HidePlayer))]
    internal static class RemoveWallCollisionHidePlayerPatch
    {
        private static void Postfix(MyPlayer __instance)
        {
            if (!Engine.Enabled<RemoveWallCollisionFeature>())
                return;

            RemoveWallCollisionLogic.ApplyColliderTo(__instance, asTrigger: true);
        }
    }
}
