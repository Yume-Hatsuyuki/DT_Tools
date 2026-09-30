using System;
using System.Linq;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>GET /api/commands — 命令表（主名去重、按名称排序），供前端预览与补全。</summary>
    public static class CommandsApi
    {
        // 命令表在 CommandRegistry.Load 后只增不改、进程内恒定，故缓存一次即可；
        // 前提变更（未来支持动态注册命令）时需同步加失效逻辑。
        private static string _cachedJson;

        public static void HandleList(HttpListenerContext ctx)
        {
            if (_cachedJson == null)
            {
                _cachedJson = Json.To(
                    CommandRegistry.All
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(c => new
                        {
                            name = c.Name,
                            aliases = c.Aliases ?? Array.Empty<string>(),
                            usage = c.Usage,
                            description = c.Description,
                            author = string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author,
                        }));
            }
            HttpServer.WriteRaw(ctx.Response, 200, "application/json; charset=utf-8", _cachedJson);
        }
    }
}
