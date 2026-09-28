using DT_Tools.Game;

namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// 点播执行：调用 Game 层的 AudioPlayback 引擎（与「StageMusic」补丁功能零引用——
    /// 播放器/缓存/限长/来源解析全部内聚在 Game/AudioPlayback）。
    /// 来源类型由 Args 显式指定（local/online/自动）；解析失败引擎只告警，
    /// 不打断当前播放。
    /// </summary>
    internal static class PlayAudioLogic
    {
        public static void Execute(PlayAudioArgs args)
        {
            AudioPlayback.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
