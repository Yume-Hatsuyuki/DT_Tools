namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// /play_audio [local|online] &lt;url|路径&gt; [seconds] [volume] — 控制台点播（仅本地播放）：
    /// 来源可显式指定（local=本地文件 / online=在线链接），缺省按前缀自动识别。
    /// 参数与「StageMusic」的阶段配置完全解耦：默认完整播放、响度 1，显式传参可覆盖；
    /// 引擎在 Game/AudioPlayback，点播与阶段音乐互不抢占，结束时也不恢复阶段音乐；
    /// /stop_music 停止点播。对全房间广播（虚拟麦克风）见 /mic_music。
    /// </summary>
    internal sealed class PlayAudioCommand : ICommand
    {
        public string Name => "play_audio";
        public string[] Aliases => new[] { "点歌", "放歌" };
        public string Usage => "play_audio [local|online] <url|路径> [seconds] [volume]";
        public string Description => "点播音频（本地/在线，默认自动识别，仅本地播放）：默认完整播放、响度 1，可显式指定来源与时长/响度，与「StageMusic」阶段音乐相互独立。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: /{Usage}\n"
                    + "local/online：显式指定来源类型，省略则自动识别；\n"
                    + "seconds：时长上限（秒），省略或 -1/0=完整播放；volume：响度 0~1，省略=1。\n"
                    + "停止播放用 /stop_music。");
                return CommandResult.Fail("missing source");
            }

            if (!PlayAudioArgs.TryParse(ctx.Args, out var args, out string error))
            {
                ctx.Reply(error);
                return CommandResult.Fail("invalid args");
            }

            PlayAudioLogic.Execute(args);
            ctx.Reply(PlayAudioFormat.Reply(args));
            return CommandResult.Success(new
            {
                source = args.Source,
                sourceKind = args.SourceKind.ToString(),
                maxSeconds = args.MaxSeconds,
                volume = args.Volume,
            });
        }
    }
}
