using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.EmoteSlot16
{
    [PatchFeature(
        "表情栏位扩展：表情槽从 8 个扩到 32 个（4 页每页 8 个）；游戏内按翻页键切换页面，商店配置页用鼠标中键翻页（客户端，自己装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerEmote | FeatureTag.UiShop,
        Author = "花语")]
    public sealed class EmoteSlot16Feature
    {
        [Config("游戏内表情板：切换下一页按键（Unity KeyCode 名称，如 Q/E/Tab）。")]
        public static string PageNextKey = "Q";

        [Config("游戏内表情板：切换上一页按键（Unity KeyCode 名称）。")]
        public static string PagePrevKey = "E";
    }
}
