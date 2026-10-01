using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.RemoveWallCollision
{
    /// <summary>
    /// 穿墙：仅对本地玩家两个碰撞体改 trigger（精确逐碰撞体处理）。
    /// 刻意不动 Physics2D.IgnoreLayerCollision——那是全局层碰撞开关，会把同机其他玩家一起陪绑穿墙。
    /// </summary>
    internal static class RemoveWallCollisionLogic
    {
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
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            ApplyColliderTo(my, asTrigger: true);
            Log.Info<RemoveWallCollisionFeature>(
                my != null ? "已对本地玩家应用穿墙" : "尚无本地玩家，待 Init 时再套用");
        }

        /// <summary>
        /// Enabled 关闭：恢复碰撞体。若本机玩家正处于躲藏态则保持 trigger——
        /// 原版 HidePlayer(true) 就是把碰撞体置为 trigger（0.1.15b MyPlayer.cs:1009，:1016 写
        /// isTrigger=isHide），此时强改回实心会与掩体碰撞体互相挤压、可能把玩家挤出掩体，
        /// 服务器也可能判非法位置；退出躲藏时原版 HidePlayer(false) 会自然恢复实心。
        /// </summary>
        public static void RestoreLocalPlayer()
        {
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
                my != null ? "已恢复本地玩家碰撞" : "当前无本地玩家");
        }
    }
}
