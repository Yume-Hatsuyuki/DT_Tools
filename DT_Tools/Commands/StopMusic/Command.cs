using System;
using DT_Tools.Core;
using DT_Tools.Patches.Fun.StageMusic;

namespace DT_Tools.Commands.StopMusic
{
    /// <summary>
    /// /stop_music — 立即停止当前正在播放的自定义音乐（阶段音乐/击杀/递刀）。
    /// 调用 StageMusicPlayer 的公开静态方法（命令域引用功能公开状态的既定例外）。
    /// </summary>
    internal sealed class StopMusicCommand : ICommand
    {
        public string Name => "stop_music";
        public string[] Aliases => new[] { "停音乐" };
        public string Usage => "stop_music";
        public string Description => "立即停止当前正在播放的自定义音乐（阶段音乐）。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            StageMusicPlayer.StopCurrent();
            ctx.Reply("已停止自定义音乐。");
            return CommandResult.Success(new { stopped = true });
        }
    }
}
