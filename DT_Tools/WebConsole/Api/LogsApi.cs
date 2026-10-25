using System.Linq;
using System.Net;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    public static class LogsApi
    {
        public static void Handle(HttpListenerContext ctx)
        {
            long.TryParse(ctx.Request.QueryString["since"], out var since);

            var (_, entries) = Log.SnapshotEntries();
            if (entries.Length > 0 && entries[0].Seq > since + 1)
                since = entries[0].Seq - 1;
            var payload = entries
                .Where(e => e.Seq > since)
                .Select(Shape);

            HttpServer.WriteJson(ctx.Response, payload);
        }

        internal static object Shape(LogEntry e) => new
        {
            seq = e.Seq,
            time = e.Time.ToString("HH:mm:ss"),
            level = e.Level,
            tag = e.Tag,
            msg = e.Message,
            session = e.Session,
        };
    }
}
