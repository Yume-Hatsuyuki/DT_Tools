using Protocol;
using UnityEngine;

namespace DT_Tools.Features.RemoveWallCollision
{
    internal static class RemoveWallCollisionLogic
    {
        public static void ApplyColliderTo(MyPlayer player, bool asTrigger)
        {
            if (player == null)
                return;
            if (player.Collider != null)
                player.Collider.isTrigger = asTrigger;
            if (player.PlayerCollider != null)
                player.PlayerCollider.isTrigger = asTrigger;
        }

        public static void ApplyToLocalPlayer()
        {
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            ApplyColliderTo(my, asTrigger: true);
            Log.Info<RemoveWallCollisionFeature>(
                my != null ? "已对本地玩家应用穿墙" : "尚无本地玩家，待 Init 时再套用");
        }

        public static void RestoreLocalPlayer()
        {
            MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
            if (my != null && my.State == EPlayerState.Hide)
            {
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
