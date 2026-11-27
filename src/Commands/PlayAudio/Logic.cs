// 作用：实现 /PlayAudio 命令的业务逻辑；不负责命令入口与注册。
using DT_Tools.Game;

namespace DT_Tools.Commands.PlayAudio
{
    internal static class PlayAudioLogic
    {
        public static bool Execute(AudioSourceArgs args)
        {
            return AudioPlayback.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
