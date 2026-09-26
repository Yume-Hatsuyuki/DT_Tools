using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.RemoveWallCollision
{
    /// <summary>
    /// MyPlayer.Init 后置：进局生成本地玩家后立即穿墙。
    /// 公共方法（nameof 定位），0.1.15b MyPlayer.cs:306；原版在 :312 写 Collider.isTrigger=false。
    /// 直接对 __instance 套用——此时 MyPlayer 可能尚未注册进 Managers.Player。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.Init))]
    internal static class RemoveWallCollisionInitPatch
    {
        private static void Postfix(MyPlayer __instance, bool __result)
        {
            if (!Engine.Enabled<RemoveWallCollisionFeature>())
                return;

            if (!__result)
                return;

            RemoveWallCollisionLogic.ApplyIgnoreLayers(ignore: true);
            RemoveWallCollisionLogic.ApplyColliderTo(__instance, asTrigger: true);
        }
    }
}
