using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.GhostView
{
    /// <summary>
    /// 幽灵视角：IsGhostView 恒 true（原版为 !Managers.Game.IsAlive）。
    /// 调试向，可能干扰状态机。
    /// </summary>
    [PatchFeature(
        "幽灵视角：（⚠️奇怪的功能）化身幽灵。可能影响正常对局，建议仅本地调试。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class GhostViewFeature
    {
    }
}
