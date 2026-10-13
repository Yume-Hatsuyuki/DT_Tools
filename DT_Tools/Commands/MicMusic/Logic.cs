using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    /// <summary>
    /// 调用 Game 层的 MicBroadcast 引擎（虚拟麦克风广播，与「StageMusic」阶段音乐、
    /// /play_audio 点播零引用互不抢占）。off 时停止广播并还原采集管线。
    /// 返回是否受理：来源解析失败= false（原因已在引擎内告警，不改动进行中的广播）。
    /// </summary>
    internal static class MicMusicLogic
    {
        public static bool Execute(MicMusicArgs args)
        {
            if (args.Off)
            {
                MicBroadcast.Stop();
                return true;
            }
            return MicBroadcast.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
