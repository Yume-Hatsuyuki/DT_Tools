using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ShowTrueName
{
    [PatchFeature(
        "吾之真名：进入游戏后头顶名字持续可见。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerIdentity,
        Author = "梦初雪")]
    public sealed class ShowTrueNameFeature
    {
        [Config("死亡后是否继续显示自己头顶名字（默认 false）。")]
        public static bool ShowWhileDead = false;
    }
}
