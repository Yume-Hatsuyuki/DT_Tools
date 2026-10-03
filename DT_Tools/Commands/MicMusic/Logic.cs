using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    /// <summary>
    /// 调用 Game 层的 MicBroadcast 引擎（虚拟麦克风广播，与「StageMusic」阶段音乐、
    /// /play_audio 点播零引用互不抢占）。off 时停止广播并还原采集管线。
    /// </summary>
    internal static class MicMusicLogic
    {
        public static void Execute(MicMusicArgs args)
        {
            if (args.Off)
            {
                MicBroadcast.Stop();
                return;
            }
            MicBroadcast.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
