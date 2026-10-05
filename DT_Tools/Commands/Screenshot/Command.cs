using DT_Tools.Commands;
using DT_Tools.Core;

namespace DT_Tools.Commands.Screenshot
{
    /// <summary>
    /// /shot [长边像素] — 截取当前画面存为 PNG（长边默认 1280，上限 4096），返回绝对路径。
    /// 游戏用 URP：帧末（WaitForEndOfFrame）读背缓冲是唯一合法的"含 UI 整帧"捕获点，而它
    /// 必然晚于命令返回——本命令经 ctx.Defer() 延迟完成：占位返回后协程在帧末捕获，回填后
    /// 等待方（WebUI /api/run、MCP run_command）一次拿到路径。不支持等待的通道（游戏内聊天）
    /// 退化为只发起，路径进日志（read_log filter=shot 查询）。
    /// </summary>
    internal sealed class ScreenshotCommand : ICommand
    {
        public string Name => "shot";
        public string[] Aliases => new[] { "screenshot" };
        public string Usage => "shot [长边像素]";
        public string Description => "截图当前画面存为 PNG，返回绝对路径（长边默认 1280；含 UI 整帧，等待帧末完成后一次返回）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            int longEdge = 1280;
            if (ctx.Args.Length > 0
                && (!int.TryParse(ctx.Args[0], out longEdge) || longEdge < 64 || longEdge > 4096))
            {
                ctx.Reply("长边像素需为 64–4096 的整数。用法: " + Usage);
                return CommandResult.Fail("invalid size");
            }

            var deferred = ctx.Defer();
            if (deferred == null)
            {
                // 通道不支持等待（游戏内聊天等）：只发起，完成时路径进日志
                ctx.Reply("本通道不支持等待截图完成，已发起帧捕获：稍后 read_log filter=shot 查路径。");
                ScreenshotLogic.Capture(longEdge, (path, _, _, blank, error) =>
                {
                    if (error != null)
                        ctx.Warn($"截图失败: {error}");
                    else if (blank)
                        ctx.Warn($"画面纯色无内容 → {path}");
                    else
                        ctx.Reply($"已截图[screen] → {path}");
                });
                return CommandResult.Success(new { scheduled = true });
            }

            ScreenshotLogic.Capture(longEdge, (path, width, height, blank, error) =>
            {
                CommandResult final;
                if (error != null)
                {
                    ctx.Warn($"截图失败: {error}");
                    final = CommandResult.Fail("capture failed");
                }
                else if (blank)
                {
                    ctx.Warn($"画面纯色无内容（窗口最小化或静止加载页）→ {path}");
                    final = CommandResult.Success(new { path, width, height, mode = "screen+blank" });
                }
                else
                {
                    ctx.Reply($"已截图[screen] → {path}");
                    final = CommandResult.Success(new { path, width, height, mode = "screen" });
                }
                deferred.Complete(final);
            });
            return deferred;
        }
    }
}
