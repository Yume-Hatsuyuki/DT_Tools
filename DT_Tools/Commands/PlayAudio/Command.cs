using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Patches.Fun.StageMusic;

namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// /play_audio &lt;url|路径&gt; — 手动点播音频：本地播放 + 经麦克风广播给所有人
    /// （需启用「StageMusic」并开启 MicBroadcast，且麦克风未静音；注入点与游戏阶段无关）。
    /// 点播期间独占播放槽（阶段音乐触发被忽略），时长受「StageMusic」功能的 MaxSeconds
    /// 配置限制（WebUI CONFIG 页 / .cfg [StageMusic] 段），结束后自动恢复当前阶段音乐。
    /// /stop_music 可随时停止。
    /// </summary>
    internal sealed class PlayAudioCommand : ICommand
    {
        public string Name => "play_audio";
        public string[] Aliases => new[] { "点歌", "放歌" };
        public string Usage => "play_audio <url|路径>";
        public string Description => "手动点播音频：本地播放；开启麦克风广播时同时发给所有人。点播期间独占播放槽，时长受「StageMusic」的 MaxSeconds 配置限制（CONFIG 页可调，-1/0 不限），结束后恢复阶段音乐。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /play_audio <url|路径>\n停止播放用 /stop_music。");
                return CommandResult.Fail("missing source");
            }

            string raw = string.Join(" ", ctx.Args);
            StageMusicPlayer.PlayManual(raw);
            ctx.Reply($"已点播：{raw}\n本地即播；点播期间独占播放槽，时长受「StageMusic」的 MaxSeconds 配置限制（WebUI CONFIG 页可调，-1/0 不限），结束后自动恢复阶段音乐；麦克风广播需启用「StageMusic」并开启 MicBroadcast、麦克风未静音。");
            return CommandResult.Success(new { source = raw });
        }
    }
}
