using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 照片发送器（PhotoSend）：游戏内聊天输入 `!photo &lt;文件名&gt;`，把本地目录
    /// （BepInEx/plugins/DT_Tools/Photos/）里的图片作为照片发送给全房。
    ///
    /// 实现：读图（LoadImage）→ 缩放（保证 JPEG ≤160KB 原版上限）→ 注入
    /// PhotoManager._shots（本地相册，Key 负数与原版一致）→ 调用原版
    /// SubmitPhoto(slotIndex) 发送 C_CHAT_PHOTO → 房主转 S_CHAT_PHOTO 全房广播。
    /// 完全走原版照片协议：无需房主权限、无需对方安装本 mod，庭审阶段也可直接发。
    /// </summary>
    [PatchFeature(
        "照片发送器：聊天输入 !photo 图片名，把 DT_Tools/Photos/ 目录里的本地图片作为照片发送给全房。\n" +
        "走原版照片广播，全房可见，无需房主或对方安装本 MOD，庭审阶段可直接发。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class PhotoSendFeature
    {
        [Config("照片长边像素：发送前把图片缩放到该尺寸（越大越清晰，注意 160KB 大小上限）。", Min = 128, Max = 1024)]
        public static int MaxSide = 512;

        [Config("JPEG 质量（0-100），越小体积越小。", Min = 30, Max = 95)]
        public static int JpegQuality = 75;
    }
}
