// 作用：为移除墙体碰撞挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.RemoveWallCollision
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.Init))]
    internal static class RemoveWallCollisionInitPatch
    {
        private static void Postfix(MyPlayer __instance, bool __result)
        {
            if (!Engine.Enabled<RemoveWallCollisionFeature>())
                return;

            if (!__result)
                return;

            RemoveWallCollisionLogic.ApplyColliderTo(__instance, asTrigger: true);
        }
    }
}
