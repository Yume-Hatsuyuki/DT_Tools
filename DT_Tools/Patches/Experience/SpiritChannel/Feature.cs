using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.SpiritChannel
{
    /// <summary>
    /// 通灵：感知死者与亡语。
    /// 子功能：
    ///   - 亡者呢喃：活人可见死亡玩家的普通聊天（原 GhostWhisper；
    ///     原版 EnqueueNormalChat 仅本地也死亡时才入 _deadMessageQueue，0.1.16b VoiceManager.cs:1331）。
    ///   - 死亡感知：他人尸体经 S_SPAWN_DEVICE 落地时，套用 Lian（SoulSense）的蜡烛 UI
    ///     （UI_SoulSence / S_NOTIFY_DEAD 链路，0.1.16b PacketHandler.cs:586、Server.Game/Player.cs:935）。
    /// 容错：本机已是 Lian（SkillData.Type == SoulSense）时不拦截原版 S_NOTIFY_DEAD，
    /// 死亡感知补丁直接跳过，避免双动画。
    /// </summary>
    [PatchFeature(
        "通灵：亡者呢喃（活人可见死聊）与死亡感知（尸体落地时播放 Lian 蜡烛动画）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class SpiritChannelFeature
    {
        [Config("亡者呢喃：活人可见死亡玩家的普通聊天（庭审/死聊回响）。")]
        public static bool GhostWhisper = true;

        [Config("死亡感知：他人尸体落地时播放 Lian 的蜡烛燃烧动画（套用 UI_SoulSence）。")]
        public static bool DeathSense = true;
    }
}
