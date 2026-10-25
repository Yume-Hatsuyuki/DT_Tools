using DT_Tools.Core.Attributes;
using System;
using DT_Tools.Game;

namespace DT_Tools.Commands.StopMusic
{
    internal sealed class StopMusicCommand : ICommand
    {
        public string Name => "stop_music";
        public string[] Aliases => new[] { "停音乐" };
        public string Usage => "stop_music";
        public string Description => "立即停止控制台点播（/play_audio）与虚拟麦克风广播（/mic_music）的音乐。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysAudio;
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            AudioPlayback.Stop();
            MicBroadcast.Stop();
            ctx.Reply("已停止点播与虚拟麦克风广播。");
            return CommandResult.Success(new { stopped = true });
        }
    }
}
