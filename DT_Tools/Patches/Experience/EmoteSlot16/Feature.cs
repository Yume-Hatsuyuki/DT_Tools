using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.EmoteSlot16
{
    /// <summary>
    /// 表情栏位扩展——8 槽 → 32 槽（4 页，每页 8 个）：
    ///   - 存档数据：EquippedEmoticonIds 扩为 32（老 8/16 槽存档自动迁移，原槽位保留、新槽位为空）
    ///   - 游戏内表情板（UI_EmotionPanel）：按页显示 8 个，游戏内按翻页键切换（可配置，默认 Q/E）
    ///   - 商店配置页（UI_Shop_EmoticonCustom）：同样 4 页 8 槽，鼠标中键点击径向板翻页
    /// 仅客户端本地生效（FeatureSide.Client），无需房主装。
    /// </summary>
    [PatchFeature(
        "表情栏位扩展：表情槽从 8 个扩到 32 个（4 页每页 8 个）；游戏内按翻页键切换页面，商店配置页用鼠标中键翻页（客户端，自己装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class EmoteSlot16Feature
    {
        [Config("游戏内表情板：切换下一页按键（Unity KeyCode 名称，如 Q/E/Tab）。")]
        public static string PageNextKey = "Q";

        [Config("游戏内表情板：切换上一页按键（Unity KeyCode 名称）。")]
        public static string PagePrevKey = "E";
    }
}
