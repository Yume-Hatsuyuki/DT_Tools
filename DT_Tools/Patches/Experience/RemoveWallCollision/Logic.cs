using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.RemoveWallCollision
{
    /// <summary>
    /// 层碰撞与碰撞体开关：Player/Wall/Block 三层互 ignore，本地玩家两个碰撞体改 trigger。
    /// </summary>
    internal static class RemoveWallCollisionLogic
    {
        // Define.ELayer：Player=7, Wall=9, Block=12（数值层号，游戏枚举在 0.1.15b Define.cs 的 ELayer）
        private const int LayerPlayer = 7;
        private const int LayerWall = 9;
        private const int LayerBlock = 12;

        public static void ApplyIgnoreLayers(bool ignore)
        {
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerWall, ignore);
            Physics2D.IgnoreLayerCollision(LayerPlayer, LayerBlock, ignore);
        }

        public static void ApplyColliderTo(MyPlayer player, bool asTrigger)
        {
            if (player == null)
                return;
            // BaseObject.Collider（0.1.15b BaseObject.cs:8）与 MyPlayer.PlayerCollider（0.1.15b MyPlayer.cs:65）
            if (player.Collider != null)
                player.Collider.isTrigger = asTrigger;
            if (player.PlayerCollider != null)
                player.PlayerCollider.isTrigger = asTrigger;
        }

        /// <summary>Enabled 热开启：对当前场景的本地玩家立即生效。</summary>
        public static void ApplyToLocalPlayer()
        {
            ApplyIgnoreLayers(ignore: true);
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            ApplyColliderTo(my, asTrigger: true);
            Log.Info<RemoveWallCollisionFeature>(
                my != null ? "已对本地玩家应用穿墙" : "层碰撞已忽略（尚无本地玩家，待 Init 时再套用）");
        }

        /// <summary>
        /// Enabled 关闭：恢复层与碰撞体。若本机玩家正处于躲藏态则保持 trigger、仅恢复层碰撞——
        /// 原版 HidePlayer(true) 就是把碰撞体置为 trigger（0.1.15b MyPlayer.cs:1009，:1016 写
        /// isTrigger=isHide），此时强改回实心会与掩体碰撞体互相挤压、可能把玩家挤出掩体，
        /// 服务器也可能判非法位置；退出躲藏时原版 HidePlayer(false) 会自然恢复实心。
        /// </summary>
        public static void RestoreLocalPlayer()
        {
            ApplyIgnoreLayers(ignore: false);
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            if (my != null && my.State == EPlayerState.Hide)
            {
                // State：0.1.15b Player.cs:438（public EPlayerState State，取 PublicInfo.State）；
                // EPlayerState.Hide：0.1.15b Protocol/EPlayerState.cs:20
                Log.Info<RemoveWallCollisionFeature>(
                    "本地玩家处于躲藏态，碰撞体保持 trigger，退出躲藏后由原版恢复实心");
                return;
            }
            ApplyColliderTo(my, asTrigger: false);
            Log.Info<RemoveWallCollisionFeature>(
                my != null ? "已恢复本地玩家碰撞" : "层碰撞已恢复（当前无本地玩家）");
        }
    }
}
