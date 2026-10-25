using DT_Tools.Core.Attributes;
using UnityEngine;

namespace DT_Tools.Features.PhotoTools
{
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

    [PatchFeature(
        "拍照美化：可选照片滤镜（黑白/复古/冷色/反转/拍立得/暗角）；幽灵相机（死后也能拍，记录凶手动向）。处理后的照片全房可见，接收方无需装 mod。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameEvidence | FeatureTag.GameGhost,
        Author = "花语")]
    public sealed class PhotoToolsFeature
    {
        [Config("照片滤镜：None=原版；BlackWhite=黑白；Retro=复古泛黄；Cool=冷色；Invert=反转负片；Polaroid=拍立得白边框；Vignette=暗角。")]
        public static PhotoFilterType Filter = PhotoFilterType.None;

        [Config("幽灵相机：死亡后（幽灵视角）也能进入拍照模式（原版要求存活）。")]
        public static bool GhostCamera = false;
    }
}
