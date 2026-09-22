using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 移除本地玩家与墙体的物理阻挡，便于在地图内自由走动。
    /// 仅客户端：走路靠 Rigidbody + PhysicsCollider；Host 仍按 MapArray 格校验，
    /// 落入 MapArray==0 的格子会被拉到 ErrorPos。
    ///
    /// 应用时机：MyPlayer.Init / HidePlayer 出口，以及 Enabled 热开启时对当前本地玩家补一次。
    /// 大厅开启后进局即可生效；若进生存阶段后才打开，会通过 OnEnabled 立即套用到已有 MyPlayer。
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

        private static void ApplyIgnoreLayers(bool ignore)
        {
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerWall, ignore);
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerBlock, ignore);
        }

        private static void ApplyCollider(MyPlayer player, bool asTrigger)
        {
            if (player == null)
                return;
            if (player.Collider != null)
                player.Collider.isTrigger = asTrigger;
            if (player.PlayerCollider != null)
                player.PlayerCollider.isTrigger = asTrigger;
        }

        private static void ApplyToLocalPlayer()
        {
            ApplyIgnoreLayers(ignore: true);
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            ApplyCollider(my, asTrigger: true);
            FeatureLogRegistry.Info(
                "RemoveWallCollision",
                my != null ? "已对本地玩家应用穿墙" : "层碰撞已忽略（尚无本地玩家，待 Init 时再套用）");
        }

        private static void RestoreLocalPlayer()
        {
            ApplyIgnoreLayers(ignore: false);
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            ApplyCollider(my, asTrigger: false);
            FeatureLogRegistry.Info(
                "RemoveWallCollision",
                my != null ? "已恢复本地玩家碰撞" : "层碰撞已恢复（当前无本地玩家）");
        }

        /// <summary>运行时打开：立刻对当前场景生效。</summary>
        public static void OnEnabled() => ApplyToLocalPlayer();

        /// <summary>运行时关闭：恢复层与碰撞体。</summary>
        public static void OnDisabled() => RestoreLocalPlayer();

        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.Init))]
        [HarmonyPostfix]
        private static void PostfixInit(MyPlayer __instance, bool __result)
        {
            if (!FeatureGate.Enabled(typeof(RemoveWallCollisionFeature)))
                return;

            if (!__result)
                return;
            ApplyIgnoreLayers(ignore: true);
            ApplyCollider(__instance, asTrigger: true);
        }

        /// <summary>
        /// 原版 HidePlayer：退出躲藏时 isTrigger=false，会重新变成实心。
        /// </summary>
        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.HidePlayer))]
        [HarmonyPostfix]
        private static void PostfixHidePlayer(MyPlayer __instance)
        {
            if (!FeatureGate.Enabled(typeof(RemoveWallCollisionFeature)))
                return;

            ApplyCollider(__instance, asTrigger: true);
        }
    }
}
