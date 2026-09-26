using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.RemoveWallCollision
{
    /// <summary>
    /// 原版 HidePlayer：退出躲藏时 isTrigger=false，会重新变成实心。
    /// 公共方法（nameof 定位），0.1.15b MyPlayer.cs:1009（:1016 写 Collider.isTrigger=isHide）。
    /// </summary>
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
