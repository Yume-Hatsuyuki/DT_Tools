using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.GhostWhisper
{
    /// <summary>
    /// 亡者呢喃：活人可见死聊。原版 EnqueueNormalChat 仅在本地玩家也死亡时
    /// 才把死亡发送者的消息入 _deadMessageQueue（0.1.15b VoiceManager.cs:1331），
    /// 此处发送者死亡则一律入队。
    /// </summary>
    [PatchFeature(
        "亡者呢喃：那些死者的回响依附在你的身边。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class GhostWhisperFeature
    {
    }
}
