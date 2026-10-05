using DT_Tools.Core.Attributes;
using UnityEngine;

namespace DT_Tools.Patches.Fun.PhotoTools
{
    /// <summary>照片滤镜类型（拍照时对截图应用，处理后的照片全房可见）。</summary>
    public enum PhotoFilterType
    {
        None = 0,
        BlackWhite,
        Retro,
        Cool,
        Invert,
        Polaroid,
        Vignette
    }

    /// <summary>
    /// 拍照工具（拍照美化）：
    /// 1. Filter：拍照时对截图应用滤镜（黑白/复古/冷色/反转/拍立得边框/暗角），
    ///    处理发生在本地 JPEG 编码前，发送给全房的照片就是 P 完的效果，
    ///    接收方无需安装本 mod（照片是游戏原生 C_CHAT_PHOTO 全房广播）；
    /// 2. GhostCamera：死亡后（幽灵视角）也能进入拍照模式，记录凶手动向交给队友。
    /// 胶卷次数与原版一致（调查阶段 3 张），阶段限制与原版一致（仅调查阶段可拍）。
    /// 发送防刷限速（约 5 秒一张）与单张 160KB 上限为游戏原生，保留不变。
    /// </summary>
    [PatchFeature(
        "拍照美化：可选照片滤镜（黑白/复古/冷色/反转/拍立得/暗角）；幽灵相机（死后也能拍，记录凶手动向）。处理后的照片全房可见，接收方无需装 mod。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class PhotoToolsFeature
    {
        [Config("照片滤镜：None=原版；BlackWhite=黑白；Retro=复古泛黄；Cool=冷色；Invert=反转负片；Polaroid=拍立得白边框；Vignette=暗角。")]
        public static PhotoFilterType Filter = PhotoFilterType.None;

        [Config("幽灵相机：死亡后（幽灵视角）也能进入拍照模式（原版要求存活）。")]
        public static bool GhostCamera = false;
    }
}
