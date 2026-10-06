using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>照片缩放模式。</summary>
    public enum ScaleModeType
    {
        /// <summary>按分辨率自适应：大图缩小到长边 MaxSide，小图放大到短边 MinSide（推荐）。</summary>
        Auto = 0,
        /// <summary>固定：只把超过 MaxSide 的大图缩小，小图不放大。</summary>
        Fixed,
        /// <summary>原样：尽量保持原分辨率（仅靠降质量压体积，超 160KB 会报错）。</summary>
        Original
    }

    /// <summary>
    /// 照片发送器（PhotoSend）：游戏内聊天输入 `![文字](路径)`（严格 markdown 括号对），
    /// 文字非空时先发一条普通聊天消息再从路径直发照片；文字可空（`![](路径)`），路径
    /// 不允许为空。路径支持本地文件、相对图片目录（BepInEx/plugins/DT_Tools/Photos/）
    /// 的文件名或 http(s) 链接。
    ///
    /// 实现：读图（LoadImage）→ 缩放（保证 JPEG ≤160KB 原版上限）→ 构造 C_CHAT_PHOTO
    /// 直发（SlotIndex=-1，不触碰相册）→ 房主转 S_CHAT_PHOTO 全房广播。
    /// 完全走原版照片协议：无需房主权限、无需对方安装本 mod，庭审阶段也可直接发。
    /// </summary>
    [PatchFeature(
        "照片发送器：聊天输入 ![文字](路径)（markdown 格式），文字非空先发一条文字再直发照片，文字可空、路径必填；" +
        "图片支持本地路径、相对图片目录的文件名或 http(s) 网络链接。\n" +
        "走原版照片广播，全房可见，无需房主或对方安装本 MOD，庭审阶段可直接发。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语+梦初雪")]
    public sealed class PhotoSendFeature
    {
        [Config("图片文件夹路径（绝对路径，例如 D:/我的照片）。留空则用默认目录 BepInEx/plugins/DT_Tools/Photos/。")]
        public static string PhotoFolder = "";

        [Config("缩放模式：Auto=按分辨率自适应（大图缩小/小图放大，推荐）；Fixed=只缩大图不放大；Original=尽量保原分辨率。")]
        public static ScaleModeType ScaleMode = ScaleModeType.Auto;

        [Config("照片长边像素：大图缩放到的上限（越大越清晰，注意 160KB 大小上限）。", Min = 128, Max = 1024)]
        public static int MaxSide = 512;

        [Config("照片短边像素：Auto 模式下小图放大到的下限（避免小图发出去太小）。", Min = 64, Max = 512)]
        public static int MinSide = 96;

        [Config("JPEG 质量（0-100），越小体积越小。", Min = 30, Max = 95)]
        public static int JpegQuality = 75;
    }
}
