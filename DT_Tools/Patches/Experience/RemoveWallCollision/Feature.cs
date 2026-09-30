using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.RemoveWallCollision
{
    /// <summary>
    /// 移除本地玩家与墙体的物理阻挡，便于在地图内自由走动。
    /// 仅客户端：走路靠 Rigidbody + PhysicsCollider；Host 仍按 MapArray 格校验，
    /// 落入 MapArray==0 的格子会被拉到 ErrorPos。
    ///
    /// 应用时机：MyPlayer.Init / HidePlayer 出口，以及 Enabled 热开启时对当前本地玩家补一次。
    /// 大厅开启后进局即可生效；若进生存阶段后才打开，会通过 OnEnabled 立即套用到已有 MyPlayer。
    /// </summary>
    [PatchFeature(
        "移除墙体碰撞：本地玩家可穿墙移动（客户端）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class RemoveWallCollisionFeature
    {
        private static void OnEnabled() => RemoveWallCollisionLogic.ApplyToLocalPlayer();

        /// <summary>运行时关闭：恢复层与碰撞体（躲藏态下保持 trigger，退出躲藏后由原版恢复实心）。</summary>
        private static void OnDisabled() => RemoveWallCollisionLogic.RestoreLocalPlayer();
    }
}
