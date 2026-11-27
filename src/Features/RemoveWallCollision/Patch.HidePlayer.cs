// 作用：为移除墙体碰撞挂载补丁拦截；不负责配置声明与状态存储。
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
