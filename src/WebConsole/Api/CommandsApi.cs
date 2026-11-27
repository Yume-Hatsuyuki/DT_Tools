// 作用：实现命令列表的 HTTP 接口；不负责路由注册与命令执行。
using DT_Tools.Core.Attributes;
using System;
using System.Linq;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    public static class CommandsApi
    {
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
                            tags = FeatureTagCatalog.Names(c.Tags),
                            author = string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author,
                        }));
            }
            HttpServer.WriteRaw(ctx.Response, 200, "application/json; charset=utf-8", _cachedJson);
        }
    }
}
