using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>GET /api/log?since=N — 全局控制台日志增量（旧前端契约：[{seq,time,color,msg}] 数组）。</summary>
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
                .Select(e => new
                {
                    seq = e.Seq,
                    time = e.Time.ToString("HH:mm:ss"),
                    color = ColorOf(e.Level),
                    msg = $"[{e.Tag}] {e.Message}",
                });

            HttpServer.WriteJson(ctx.Response, payload);
        }

        /// <summary>
        /// GET /api/log/stream?since=N — SSE 日志流：每 400ms 推一批增量条目（data: [entry…]），
        /// 无增量时发心跳注释行。客户端断开（写入失败）即退出；仅访问内存环形缓冲，不碰 Unity API。
        /// </summary>
        public static void HandleStream(HttpListenerContext ctx)
        {
            var resp = ctx.Response;
            resp.ContentType = "text/event-stream";
            resp.Headers["Cache-Control"] = "no-cache";
            resp.SendChunked = true;

            long.TryParse(ctx.Request.QueryString["since"], out var lastSeq);

            try
            {
                while (true)
                {
                    var (_, entries) = Log.SnapshotEntries();
                    // 只推环形缓冲里还留着的增量（seq ≤ 缓冲最旧条目时视为已截断，从缓冲头重放）
                    long oldestSeq = entries.Length > 0 ? entries[0].Seq : lastSeq;
                    if (lastSeq < oldestSeq - 1)
                        lastSeq = oldestSeq - 1;
                    var batch = entries
                        .Where(e => e.Seq > lastSeq)
                        .Select(e => new
                        {
                            seq = e.Seq,
                            time = e.Time.ToString("HH:mm:ss"),
                            color = ColorOf(e.Level),
                            msg = $"[{e.Tag}] {e.Message}",
                        })
                        .ToList();

                    string payload = batch.Count > 0
                        ? "data: " + Json.To(batch) + "\n\n"
                        : ": ping\n\n";
                    resp.OutputStream.Write(Encoding.UTF8.GetBytes(payload));
                    resp.OutputStream.Flush();
                    if (batch.Count > 0)
                        lastSeq = batch[batch.Count - 1].seq;

                    Thread.Sleep(400);
                }
            }
            catch
            {
                // 客户端断开 / 服务停止：SSE 正常结束
            }
            finally
            {
                try { resp.OutputStream.Close(); } catch { /* 已断开 */ }
            }
        }

        private static string ColorOf(string level) => level switch
        {
            "ERROR" => "red",
            "FATAL" => "red",
            "WARN" => "orange",
            "INFO" => "cyan",
            "DEBUG" => "white",
            _ => "white",
        };
    }
}
