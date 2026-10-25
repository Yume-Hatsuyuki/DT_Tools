using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PlaytestMode
{
    [PatchFeature(
        "测试模式：按测试服逻辑运行（例如可更少人数开局），库存校验仍走正式服。房主开启会改变开局人数下限/任务规模（需联机确认是否全房间生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysDebug | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class PlaytestModeFeature
    {
    }
}
