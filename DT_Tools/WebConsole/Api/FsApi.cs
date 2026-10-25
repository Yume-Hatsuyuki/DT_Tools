using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    public static class FsApi
    {
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac" };

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".webm", ".mkv", ".mov", ".m4v", ".avi" };

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
                full = Path.GetDirectoryName(full) ?? "";
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
                .Select(e => (object)new { name = e.Name, path = e.FullName, isDir = true, audio = false, video = false, size = 0L });
            var files = items.Where(e => e is FileInfo)
                .Where(e => (SafeAttributes(e) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(e => AudioExtensions.Contains(Path.GetExtension(e.Name)) ? 0 : (VideoExtensions.Contains(Path.GetExtension(e.Name)) ? 1 : 2))
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => (object)new
                {
                    name = e.Name,
                    path = e.FullName,
                    isDir = false,
                    audio = AudioExtensions.Contains(Path.GetExtension(e.Name)),
                    video = VideoExtensions.Contains(Path.GetExtension(e.Name)),
                    size = ((FileInfo)e).Length,
                });

            var entries = dirs.Concat(files).Take(MaxListEntries).ToArray();
            return new { ok = true, path = full, parent = Directory.GetParent(full)?.FullName, entries, truncated = items.Length > entries.Length };
        }

        private static FileAttributes SafeAttributes(FileSystemInfo entry)
        {
            try { return entry.Attributes; }
            catch (Exception) { return 0; }
        }
    }
}
