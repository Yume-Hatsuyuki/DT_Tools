// 作用：声明移除墙体碰撞功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.RemoveWallCollision
{
    [PatchFeature(
        "移除墙体碰撞：本地玩家可穿墙移动（客户端）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerMove,
        Author = "梦初雪")]
    public sealed class RemoveWallCollisionFeature
    {
        private static void OnEnabled() => RemoveWallCollisionLogic.ApplyToLocalPlayer();

        private static void OnDisabled() => RemoveWallCollisionLogic.RestoreLocalPlayer();
    }
}
