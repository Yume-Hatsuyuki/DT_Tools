// 作用：声明始终显示真实 SteamID 的功能开关；不实现补丁拦截与数据还原。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ShowRealSteamId
{
    [PatchFeature(
        "始终显示真实 SteamID：举报列表/详情与本地战绩记录不再被隐藏身份掩码，显示完整 SteamID64 与 Steam 昵称；开启后新生成的战绩记录以明文保存，旧记录无法还原。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.UiPopup | FeatureTag.SysData,
        Author = "梦初雪")]
    public sealed class ShowRealSteamIdFeature
    {
    }
}
