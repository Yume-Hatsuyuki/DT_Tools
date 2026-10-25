using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SpiritChannel
{
    [PatchFeature(
        "通灵：亡者呢喃（活人可见死聊）与死亡感知（尸体落地时播放 Lian 蜡烛动画）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameGhost | FeatureTag.SysAudio | FeatureTag.GameCorpse,
        Author = "梦初雪")]
    public sealed class SpiritChannelFeature
    {
        [Config("亡者呢喃：活人可见死亡玩家的普通聊天（庭审/死聊回响）。")]
        public static bool GhostWhisper = true;

        [Config("死亡感知：他人尸体落地时播放 Lian 的蜡烛燃烧动画（套用 UI_SoulSence）。")]
        public static bool DeathSense = true;
    }
}
