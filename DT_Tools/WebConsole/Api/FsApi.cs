using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// GET /api/fs/list?path=目录 — 只读列举本机目录，供 WebUI 内置文件选择器
    /// （apps/common/FileBrowser.vue）浏览。path 为空 → 列出全部盘符（根视图，
    /// parent=null）；path 指向文件 → 列出其所在目录（地址栏粘贴文件路径时能直接
    /// 落在目标位置）。隐藏/系统属性条目（$RECYCLE.BIN、desktop.ini 等）不出现在列表里。
    /// 由本机后端代为浏览是唯一可行通道：游戏 Mono 加载不了 System.Windows.Forms
    /// （曾用的原生对话框反射方案已因此整体移除），浏览器安全模型又拿不到本地绝对
    /// 路径。威胁面与 /api/run（可执行任意命令）一致：只读列举、不读文件内容，
    /// 鉴权与跨站防护由 Router 统一处理。
    /// </summary>
    public static class FsApi
    {
        /// <summary>音频扩展名，前端据此高亮可点播的文件。</summary>
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac" };

        /// <summary>单次列举上限：系统目录（如 C:\Windows）条目数以万计，截断防止响应过大。</summary>
        private const int MaxListEntries = 1000;

        public static void HandleList(HttpListenerContext ctx)
        {
            string raw = ctx.Request.QueryString["path"] ?? "";
            try
            {
                HttpServer.WriteJson(ctx.Response, ListDirectory(raw));
            }
            catch (Exception ex)
            {
                Log.Warn("WebConsole", $"目录列举失败（{raw}）: {ex.Message}");
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "list failed", message = ex.Message });
            }
        }

        private static object ListDirectory(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady)
                    .Select(d => (object)new { name = d.Name, path = d.Name, isDir = true, audio = false, size = 0L })
                    .ToArray();
                return new { ok = true, path = "", parent = (string)null, entries = drives, truncated = false };
            }

            string full;
            try { full = Path.GetFullPath(raw); }
            catch (Exception)
            {
                return new { ok = false, error = "invalid path", message = "路径无效：" + raw };
            }

            if (!Directory.Exists(full))
            {
                if (!File.Exists(full))
                    return new { ok = false, error = "not found", message = "路径不存在：" + full };
                full = Path.GetDirectoryName(full) ?? "";   // 地址栏粘的是文件路径：回退到所在目录
            }

            FileSystemInfo[] items;
            try { items = new DirectoryInfo(full).GetFileSystemInfos(); }
            catch (UnauthorizedAccessException)
            {
                return new { ok = false, error = "access denied", message = "系统拒绝访问该目录：" + full };
            }

            var dirs = items.Where(e => e is DirectoryInfo)
                .Where(e => (SafeAttributes(e) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => (object)new { name = e.Name, path = e.FullName, isDir = true, audio = false, size = 0L });
            var files = items.Where(e => e is FileInfo)
                .Where(e => (SafeAttributes(e) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(e => AudioExtensions.Contains(Path.GetExtension(e.Name)) ? 0 : 1)   // 音频排前
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => (object)new
                {
                    name = e.Name,
                    path = e.FullName,
                    isDir = false,
                    audio = AudioExtensions.Contains(Path.GetExtension(e.Name)),
                    size = ((FileInfo)e).Length,
                });

            var entries = dirs.Concat(files).Take(MaxListEntries).ToArray();
            return new { ok = true, path = full, parent = Directory.GetParent(full)?.FullName, entries, truncated = items.Length > entries.Length };
        }

        /// <summary>Attributes 属性会按需访问文件系统（断链 reparse point 会抛异常）——读失败按可见处理。</summary>
        private static FileAttributes SafeAttributes(FileSystemInfo entry)
        {
            try { return entry.Attributes; }
            catch (Exception) { return 0; }
        }
    }
}
