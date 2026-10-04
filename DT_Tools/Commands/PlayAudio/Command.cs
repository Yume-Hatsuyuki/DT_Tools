namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// /play_audio &lt;url|路径&gt; [volume 0.8] [seconds 120] [local|online] — 控制台点播（仅本地播放，
    /// 参数顺序任意，路径放最前即可）：来源可显式指定（local=本地文件 / online=在线链接），
    /// 缺省按前缀自动识别。
    /// 参数解析复用共享核心 AudioSourceArgs（带空格路径可整体加引号，非强制；
    /// 与数字参数歧义时用 volume/seconds 关键字），与「StageMusic」的阶段配置完全
    /// 解耦：默认完整播放、响度 1，显式传参可覆盖；引擎在 Game/AudioPlayback，
    /// 点播与阶段音乐互不抢占，结束时也不恢复阶段音乐；/stop_music 停止点播。
    /// 对全房间广播（虚拟麦克风）见 /mic_music。
    /// </summary>
    internal sealed class PlayAudioCommand : ICommand
    {
        public string Name => "play_audio";
        public string[] Aliases => new[] { "点歌", "放歌" };
        public string Usage => "play_audio <url|路径> [volume 0.8] [seconds 120] [local|online]";
        public string Description => "点播音频（本地/在线，默认自动识别，仅本地播放）：默认完整播放、响度 1，可显式指定来源与时长/响度，与「StageMusic」阶段音乐相互独立。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: /{Usage}\n"
                    + "参数顺序任意（像常规软件的命名参数），路径放最前即可；\n"
                    + "local/online：显式指定来源类型（位置任意），省略则自动识别；\n"
                    + "seconds：时长上限（秒），省略或 -1/0=完整播放；volume：响度 0~1，省略=1；\n"
                    + "带空格路径：整体加引号最稳（不强制，直接连写也认）；与数字参数有歧义时用关键字 volume 0.8 / seconds 120。\n"
                    + "停止播放用 /stop_music。");
                return CommandResult.Fail("missing source");
            }

            if (!AudioSourceArgs.TryParse(ctx.Args, 1f, out var args, out string error))
            {
                ctx.Reply(error);
                return CommandResult.Fail("invalid args");
            }

            if (!PlayAudioLogic.Execute(args))
            {
                ctx.Warn("未能播放：音频来源无效（文件不存在/路径非法/协议不符，原因见 AudioPlayback 日志）");
                return CommandResult.Fail("invalid source");
            }
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
