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
