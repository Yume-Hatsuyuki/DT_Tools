namespace DT_Tools.Commands.MicMusic
{
    /// <summary>
    /// /mic_music &lt;url|路径&gt; [volume 0.8] [seconds 120] [local|online] | off — 虚拟麦克风点歌
    /// （参数顺序任意，路径放最前即可）：运行期把 Dissonance 捕获管线上的采集器临时替换为
    /// 包装器，音乐混入麦克风帧后照常走语音编码发给全房间（混流点在 VAD 上游，无需说话）。
    /// 真实麦克风不被接替（仅叠加），播放结束或 off 时销毁还原。引擎在 Game/MicBroadcast；
    /// /stop_music 兼停。
    /// 前提：游戏内麦克风未静音（Comms.IsMuted=true 时 Dissonance 整条发送关闭）。
    /// </summary>
    internal sealed class MicMusicCommand : ICommand
    {
        public string Name => "mic_music";
        public string[] Aliases => new[] { "开麦点歌", "麦克风点歌" };
        public string Usage => "mic_music <url|路径> [volume 0.8] [seconds 120] [local|online] | mic_music off";
        public string Description => "虚拟麦克风点歌：音乐混入你的麦克风发给全房（无需说话），播完自动还原采集管线；off 停止。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: /{Usage}\n"
                    + "参数顺序任意（像常规软件的命名参数），路径放最前即可；\n"
                    + "local/online：显式指定来源类型（位置任意），省略则自动识别；\n"
                    + "seconds：时长上限（秒），省略或 -1/0=完整播放；volume：响度 0~1，省略=0.85（同时控制本地与混入响度，广播建议 0.6~0.9）；\n"
                    + "带空格路径：整体加引号最稳（不强制，直接连写也认）；与数字参数有歧义时用关键字 volume 0.8 / seconds 120；\n"
                    + "off：停止广播并还原采集管线（/stop_music 也兼停）；\n"
                    + "注意：游戏内麦克风需未静音、非按键说话状态，否则 Dissonance 不发送任何语音（含混入的音乐）；\n"
                    + "广播期间语音降噪/回声消除自动临时关停、Opus 码率提到 96k（结束自动还原）。");
                return CommandResult.Fail("missing source");
            }

            if (!MicMusicArgs.TryParse(ctx.Args, out var args, out string error))
            {
                ctx.Reply(error);
                return CommandResult.Fail("invalid args");
            }

            if (!MicMusicLogic.Execute(args))
            {
                ctx.Warn("未能开始广播：音频来源无效（文件不存在/路径非法/协议不符，原因见 MicBroadcast 日志）");
                return CommandResult.Fail("invalid source");
            }
            ctx.Reply(MicMusicFormat.Reply(args));
            if (args.Off)
                return CommandResult.Success(new { stopped = true });
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
