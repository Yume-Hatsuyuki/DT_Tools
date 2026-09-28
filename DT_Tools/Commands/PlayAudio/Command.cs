namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// /play_audio &lt;url|路径&gt; [seconds] [volume] — 手动点播音频：本地播放 + 可选经麦克风
    /// 广播给所有人（需启用「StageMusic」并开启 MicBroadcast，且麦克风未静音；注入点与游戏
    /// 阶段无关）。来源按前缀自动识别在线（http/https）或本地文件路径。
    /// 参数与「StageMusic」的阶段配置完全解耦：默认完整播放（不限时长）、响度 1，
    /// 显式传参可覆盖，不读取也不影响任何阶段的 [Config] 字段。
    /// 点播期间独占播放槽（阶段音乐触发被忽略），结束/限长后自动恢复当前阶段音乐；
    /// /stop_music 可随时停止。
    /// </summary>
    internal sealed class PlayAudioCommand : ICommand
    {
        public string Name => "play_audio";
        public string[] Aliases => new[] { "点歌", "放歌" };
        public string Usage => "play_audio <url|路径> [seconds] [volume]";
        public string Description => "手动点播音频（在线链接或本地路径，自动识别）：默认完整播放、响度 1，可传参覆盖时长/响度，与「StageMusic」阶段配置无关。点播期间独占播放槽，结束后恢复阶段音乐。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: /{Usage}\nseconds：时长上限（秒），省略或 -1/0=完整播放；volume：响度 0~1，省略=1。\n停止播放用 /stop_music。");
                return CommandResult.Fail("missing source");
            }

            if (!PlayAudioArgs.TryParse(ctx.Args, out var args, out string error))
            {
                ctx.Reply(error);
                return CommandResult.Fail("invalid args");
            }

            PlayAudioLogic.Execute(args);
            ctx.Reply(PlayAudioFormat.Reply(args));
            return CommandResult.Success(new { source = args.Source, maxSeconds = args.MaxSeconds, volume = args.Volume });
        }
    }
}
