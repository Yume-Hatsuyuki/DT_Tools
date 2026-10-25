using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PhotoSend
{
    public enum ScaleModeType
    {
        Auto = 0,
        Fixed,
        Original
    }

    public enum GifDecoderType
    {
        SystemDrawing = 0,
        Builtin,
    }

    [PatchFeature(
        "照片发送器：聊天输入 ![文字](路径)（markdown 格式），文字非空先发一条文字再直发照片，文字可空、路径必填；" +
        "图片支持本地路径、相对图片目录的文件名或 http(s) 网络链接；格式为 jpg / png，gif 只发第一帧（静态），解码器见配置 GifDecoder。\n" +
        "走原版照片广播，全房可见，无需房主或对方安装本 MOD，庭审阶段可直接发。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiChat,
        Author = "花语+梦初雪")]
    public sealed class PhotoSendFeature
    {
        [Config("图片文件夹路径（绝对路径，例如 D:/我的照片）。留空则用默认目录 BepInEx/config/DT_Tools/photos/。")]
        public static string PhotoFolder = "";

        [Config("缩放模式：Auto=按分辨率自适应（大图缩小/小图放大，推荐）；Fixed=只缩大图不放大；Original=尽量保原分辨率。")]
        public static ScaleModeType ScaleMode = ScaleModeType.Auto;

        [Config("照片长边像素：大图缩放到的上限（越大越清晰，注意 160KB 大小上限）。800=与原版手机拍照同分辨率。", Min = 128, Max = 1024)]
        public static int MaxSide = 800;

        [Config("照片短边像素：Auto 模式下小图放大到的下限（避免小图发出去太小）。", Min = 64, Max = 512)]
        public static int MinSide = 96;

        [Config("JPEG 质量（0-100），越小体积越小。85=接近原版拍照画质（原版 q90 起步按体积自适应下调）。", Min = 30, Max = 95)]
        public static int JpegQuality = 85;

        [Config("GIF 解码器：SystemDrawing=系统 GDI+（默认，依赖 Windows 自带的 gdiplus.dll）；Builtin=内置纯托管解码（零依赖，已逐像素验证）。下一张 GIF 起生效。")]
        public static GifDecoderType GifDecoder = GifDecoderType.SystemDrawing;
    }
}
