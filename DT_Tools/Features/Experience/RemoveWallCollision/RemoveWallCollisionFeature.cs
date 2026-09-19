using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 移除本地玩家与墙体的物理阻挡，便于在地图内自由走动。
    /// 仅客户端：走路靠 Rigidbody + PhysicsCollider；Host 仍按 MapArray 格校验，
    /// 落入 MapArray==0 的格子会被拉到 ErrorPos。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "RemoveWallCollision",
        description: "移除墙体碰撞：本地玩家可穿墙移动（客户端）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class RemoveWallCollisionFeature
    {
        // Define.ELayer：Player=7, Wall=9, Block=12
        private const int LayerPlayer = 7;
        private const int LayerWall = 9;
        private const int LayerBlock = 12;

        private static void ApplyIgnoreLayers()
        {
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerWall, ignore: true);
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerBlock, ignore: true);
        }

        private static void ApplyCollider(MyPlayer player)
        {
            if (player == null)
                return;
            if (player.Collider != null)
                player.Collider.isTrigger = true;
            if (player.PlayerCollider != null)
                player.PlayerCollider.isTrigger = true;
        }

        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.Init))]
        [HarmonyPostfix]
        private static void PostfixInit(MyPlayer __instance, bool __result)
        {
            if (!__result)
                return;
            ApplyIgnoreLayers();
            ApplyCollider(__instance);
        }

        /// <summary>
        /// 原版 HidePlayer：退出躲藏时 isTrigger=false，会重新变成实心。
        /// </summary>
        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.HidePlayer))]
        [HarmonyPostfix]
        private static void PostfixHidePlayer(MyPlayer __instance)
        {
            ApplyCollider(__instance);
        }
    }
}
