using System.Linq;
using System.Net;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>GET /api/log?since=N — 全局日志增量（兜底/首屏用；实时流走 /api/log/ws）。
    /// 形状 = 结构化条目数组 [{seq,time,level,tag,msg,session}]（color 由前端按 level 推导）。</summary>
    public static class LogsApi
    {
        public static void Handle(HttpListenerContext ctx)
        {
            long.TryParse(ctx.Request.QueryString["since"], out var since);

            var (_, entries) = Log.SnapshotEntries();
            // 环形缓冲会挤掉最旧条目：since 早于缓冲最旧条目时，从缓冲头重放而不是静默留下缺口
            if (entries.Length > 0 && entries[0].Seq > since + 1)
                since = entries[0].Seq - 1;
            var payload = entries
                .Where(e => e.Seq > since)
                .Select(Shape);

            HttpServer.WriteJson(ctx.Response, payload);
        }

        /// <summary>与 WebSocket 帧（LogStreamApi）同一份条目形状，前端只认一种协议。</summary>
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
