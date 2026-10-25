using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Core;

namespace DT_Tools.Commands.Screenshot
{
    internal sealed class ScreenshotCommand : ICommand
    {
        public string Name => "shot";
        public string[] Aliases => new[] { "screenshot" };
        public string Usage => "shot [长边像素]";
        public string Description => "截图当前画面存为 PNG，返回绝对路径（长边默认 1280；含 UI 整帧，等待帧末完成后一次返回）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysDebug;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            int longEdge = 1280;
            if (ctx.Args.Length > 0
                && (!int.TryParse(ctx.Args[0], out longEdge) || longEdge < 64 || longEdge > 4096))
            {
                ctx.Reply($"长边像素需为 64–4096 的整数。用法: {Usage}");
                return CommandResult.Fail("invalid size");
            }

            var deferred = ctx.Defer();
            if (deferred == null)
            {
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
