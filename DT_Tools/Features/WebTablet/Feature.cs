using DT_Tools.Core.Attributes;
using DT_Tools.Game.WebTablet;

namespace DT_Tools.Features.WebTablet
{
    [PatchFeature(
        "平板电脑（实验性）：WebUI 新增「平板电脑」应用，左侧聊天、右侧线索；记录阶段切换，支持导出文本或图片、清理，并可在大厅与学级裁判发言。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiTablet | FeatureTag.UiChat,
        Author = "梦初雪")]
    public sealed class WebTabletFeature
    {
        private static void OnDisabled() => WebTabletStore.Reset();
    }
}
