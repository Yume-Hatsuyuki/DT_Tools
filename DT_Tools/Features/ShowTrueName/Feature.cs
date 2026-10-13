using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ShowTrueName
{
    /// <summary>
    /// 吾之真名：本机名字常显。
    /// </summary>
    /// <remarks>
    /// ChangeMyPlayer 进对局后原版会关掉自己的 NameTag
    /// （0.1.16b PlayerManager.cs:645（待核对）)，此处强制打开；可选死后仍显（GameManagerEX.Dead，:333）。
    /// </remarks>
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
