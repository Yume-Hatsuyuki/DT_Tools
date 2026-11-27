// 作用：声明自由修改大厅预设功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ApplyLobbyPreset
{
    [PatchFeature(
        "自由修改大厅预设：改全房间规则，写入房主本地文件。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiPopup | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class ApplyLobbyPresetFeature
    {
    }
}
