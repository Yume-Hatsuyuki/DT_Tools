using DT_Tools.Patches.Fun.StageMusic;

namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// 点播执行：直接调用 StageMusicPlayer.PlayManual（命令域引用功能公开状态的既定例外，
    /// 见 AGENTS.md）。命令本身不做业务判断——是否真正播放（含麦克风广播）完全取决于
    /// StageMusicPlayer 自身状态；本命令不检查「StageMusic」的 Enabled（有意如此：
    /// 点播与阶段音乐是两条独立能力）。
    /// </summary>
    internal static class PlayAudioLogic
    {
        public static void Execute(PlayAudioArgs args)
        {
            StageMusicPlayer.PlayManual(args.Source, args.Volume, args.MaxSeconds);
        }
    }
}
