using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using DT_Tools.Core;

namespace DT_Tools.WebConsole
{
    public static class UpdateService
    {
        private const string ReleaseApiUrl =
            "https://api.github.com/repos/Yume-Hatsuyuki/DT_Tools/releases/latest";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan FailLogThrottle = TimeSpan.FromMinutes(30);
        private const int RequestTimeoutMs = 8000;
        private const int DownloadChunkBytes = 256 * 1024;

        private static readonly object Gate = new object();

        static UpdateService()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }
        }

        private static ReleaseSnapshot _cached;
        private static DateTime _checkedAt;
        private static string _lastError;
        private static DateTime _lastFailLogAt;

        private sealed class DownloadSnapshot
        {
            public readonly bool Active;
            public readonly bool Done;
            public readonly long Received;
            public readonly long Total;
            public readonly string SavePath;
            public readonly string Error;

            public DownloadSnapshot(bool active, bool done, long received, long total, string savePath, string error)
            {
                Active = active; Done = done; Received = received;
                Total = total; SavePath = savePath; Error = error;
            }
        }

        private static volatile DownloadSnapshot _download =
            new DownloadSnapshot(false, false, 0, 0, null, null);

        public static string CurrentVersion { get; } = NormalizeBuildVersion(
            typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        public static object GetStatus(bool force)
        {
            lock (Gate)
            {
                bool expired = _cached == null || DateTime.UtcNow - _checkedAt >= CacheTtl;
                if (force || expired)
                {
                    _checkedAt = DateTime.UtcNow;
                    try
                    {
                        _cached = FetchLatest();
                        _lastError = null;
                        if (force)
                            Log.Info("WebConsole", $"手动检查更新完成：最新版 {Describe(_cached)}。");
                    }
                    catch (Exception ex)
                    {
                        _lastError = DescribeError(ex);
                        bool forced = force;
                        if (forced || DateTime.UtcNow - _lastFailLogAt >= FailLogThrottle)
                        {
                            _lastFailLogAt = DateTime.UtcNow;
                            Log.Warn("WebConsole",
                                $"更新检测失败{(forced ? "（手动）" : "")}：{_lastError}" +
                                (_cached != null ? "（沿用上次结果）" : ""));
                        }
                    }
                }
                return BuildStatus();
            }
        }

        public static object StartDownload()
        {
            ReleaseSnapshot release;
            ReleaseAsset asset;
            string target;
            lock (Gate)
            {
                if (_download.Active)
                    return new { ok = false, error = "下载已在进行中" };
                release = _cached;
                if (release == null)
                    return new { ok = false, error = "尚未获取到版本信息，请先检查更新" };
                asset = PickZipAsset(release);
                if (asset == null)
                    return new { ok = false, error = "该版本没有可下载的 zip 安装包" };
                target = ResolveSavePath(asset.Name);
                _download = new DownloadSnapshot(true, false, 0, asset.Size, target, null);
            }

            var job = new DownloadJob { Url = asset.DownloadUrl, SavePath = target, Total = asset.Size };
            var thread = new Thread(RunDownload) { IsBackground = true, Name = "DT_UpdateDownload" };
            thread.Start(job);
            Log.Info("WebConsole", $"开始下载 {Describe(release)} 安装包 → {target}");
            return new { ok = true };
        }

        public static string RevealDownload()
        {
            var snap = _download;
            if (snap.SavePath == null)
                return "尚未下载过安装包";
            if (!File.Exists(snap.SavePath))
                return $"文件已不存在：{snap.SavePath}";
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return $"仅支持 Windows 资源管理器定位：{snap.SavePath}";
            try
            {
                var psi = new ProcessStartInfo("explorer.exe", "/select,\"" + snap.SavePath + "\"")
                {
                    UseShellExecute = true,
                };
                Process.Start(psi);
                return null;
            }
            catch (Exception ex)
            {
                return $"打开资源管理器失败：{ex.Message}";
            }
        }

        private static object BuildStatus()
        {
            bool hasUpdate = false;
            if (_cached != null)
                hasUpdate = IsNewer(_cached.TagName, CurrentVersion);
            return new
            {
                ok = _lastError == null,
                current = CurrentVersion,
                hasUpdate,
                latest = _cached == null ? null : new
                {
                    tag = _cached.TagName,
                    name = _cached.Name,
                    publishedAt = _cached.PublishedAt,
                    url = _cached.HtmlUrl,
                    notes = _cached.Body,
                    assets = _cached.Assets.ConvertAll(a => new
                    {
                        name = a.Name,
                        size = a.Size,
                        downloadUrl = a.DownloadUrl,
                        downloadCount = a.DownloadCount,
                    }),
                },
                lastChecked = _checkedAt == default ? (DateTime?)null : _checkedAt.ToLocalTime(),
                error = _lastError,
                download = new
                {
                    active = _download.Active,
                    done = _download.Done,
                    received = _download.Received,
                    total = _download.Total,
                    savePath = _download.SavePath,
                    error = _download.Error,
                },
            };
        }

        private static string Describe(ReleaseSnapshot r) => string.IsNullOrEmpty(r.TagName) ? "未知版本" : r.TagName;

        private static ReleaseSnapshot FetchLatest()
        {
            var request = (HttpWebRequest)WebRequest.Create(ReleaseApiUrl);
            request.Method = "GET";
            request.Timeout = RequestTimeoutMs;
            request.ReadWriteTimeout = RequestTimeoutMs;
            request.UserAgent = "DT_Tools-WebConsole";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                var release = Json.From<GitHubRelease>(reader.ReadToEnd());
                if (release == null || string.IsNullOrEmpty(release.TagName))
                    throw new Exception("GitHub 返回了无法识别的数据");
                var snapshot = new ReleaseSnapshot
                {
                    TagName = release.TagName,
                    Name = release.Name,
                    PublishedAt = release.PublishedAt,
                    HtmlUrl = release.HtmlUrl,
                    Body = release.Body,
                    Assets = new List<ReleaseAsset>(),
                };
                if (release.Assets != null)
                    foreach (var a in release.Assets)
                        snapshot.Assets.Add(new ReleaseAsset
                        {
                            Name = a.Name,
                            Size = a.Size,
                            DownloadUrl = a.DownloadUrl,
                            DownloadCount = a.DownloadCount,
                        });
                return snapshot;
            }
        }

        private static string DescribeError(Exception ex)
        {
            if (ex is WebException wex && wex.Response is HttpWebResponse resp)
            {
                try
                {
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    {
                        var body = reader.ReadToEnd();
                        if (Json.TryFrom<GitHubError>(body, out var err) && !string.IsNullOrEmpty(err.Message))
                            return err.Message;
                    }
                }
                catch { }
            }
            return ex.Message;
        }

        public static bool IsNewer(string tag, string current)
        {
            VersionKey remote = VersionKey.Parse(tag);
            VersionKey local = VersionKey.Parse(current);
            if (remote == null || local == null)
            {
                string na = (tag ?? "").Trim().TrimStart('v', 'V');
                string nb = (current ?? "").Trim().TrimStart('v', 'V');
                return na.Length > 0 && !string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
            }

            int cmp = remote.CompareNumbers(local);
            if (cmp != 0)
                return cmp > 0;
            if (remote.Revision == null || local.Revision == null)
                return false;
            return remote.Revision.Value > local.Revision.Value;
        }

        internal static string NormalizeBuildVersion(string raw)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                int plus = raw.IndexOf('+');
                string v = (plus >= 0 ? raw.Substring(0, plus) : raw).Trim();
                if (v.Length > 0)
                    return v;
            }
            return MyPluginInfo.PLUGIN_VERSION;
        }

        private sealed class VersionKey
        {
            private static readonly Regex NumberPattern =
                new Regex(@"^[vV]?(\d+(?:\.\d+)*)(.*)$", RegexOptions.CultureInvariant);
            private static readonly Regex RevisionPattern =
                new Regex(@"^[-_]?[vV]?(\d+)$", RegexOptions.CultureInvariant);

            public int[] Numbers;

            public int? Revision;

            public static VersionKey Parse(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                Match m = NumberPattern.Match(text.Trim());
                if (!m.Success)
                    return null;

                string[] parts = m.Groups[1].Value.Split('.');
                var numbers = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], out numbers[i]))
                        return null;
                }

                string suffix = m.Groups[2].Value;
                int? revision = 0;
                if (suffix.Length > 0)
                {
                    Match r = RevisionPattern.Match(suffix);
                    revision = r.Success && int.TryParse(r.Groups[1].Value, out int n) ? n : (int?)null;
                }
                return new VersionKey { Numbers = numbers, Revision = revision };
            }

            public int CompareNumbers(VersionKey other)
            {
                int len = Math.Max(Numbers.Length, other.Numbers.Length);
                for (int i = 0; i < len; i++)
                {
                    int x = i < Numbers.Length ? Numbers[i] : 0;
                    int y = i < other.Numbers.Length ? other.Numbers[i] : 0;
                    if (x != y)
                        return x.CompareTo(y);
                }
                return 0;
            }
        }

        private sealed class DownloadJob
        {
            public string Url;
            public string SavePath;
            public long Total;
        }

        private static ReleaseAsset PickZipAsset(ReleaseSnapshot release)
        {
            foreach (var a in release.Assets)
                if (!string.IsNullOrEmpty(a.Name) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return a;
            return null;
        }

        private static string ResolveSavePath(string assetName)
        {
            string fileName = Path.GetFileName(assetName);
            if (string.IsNullOrEmpty(fileName))
                fileName = $"DT_Tools-{CurrentVersion}.zip";
            try
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(downloads))
                    return Path.Combine(downloads, fileName);
            }
            catch { }
            return Path.Combine(Paths.BepInExRootPath, fileName);
        }

        private static void RunDownload(object state)
        {
            var job = (DownloadJob)state;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(job.Url);
                request.Method = "GET";
                request.Timeout = RequestTimeoutMs;
                request.ReadWriteTimeout = 30000;
                request.UserAgent = "DT_Tools-WebConsole";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var source = response.GetResponseStream())
                using (var target = new FileStream(job.SavePath, FileMode.Create, FileAccess.Write))
                {
                    long total = response.ContentLength > 0 ? response.ContentLength : job.Total;
                    var buffer = new byte[DownloadChunkBytes];
                    long received = 0;
                    while (true)
                    {
                        int read = source.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;
                        target.Write(buffer, 0, read);
                        received += read;
                        _download = new DownloadSnapshot(true, false, received, total, job.SavePath, null);
                    }
                    _download = new DownloadSnapshot(false, true, received, total, job.SavePath, null);
                    Log.Info("WebConsole", $"新版安装包下载完成（{received / 1024} KB）→ {job.SavePath}");
                }
            }
            catch (Exception ex)
            {
                string message = DescribeError(ex);
                _download = new DownloadSnapshot(false, true, 0, job.Total, job.SavePath, message);
                Log.Warn("WebConsole", $"新版安装包下载失败：{message}");
                try { File.Delete(job.SavePath); } catch { }
            }
        }

        private sealed class GitHubRelease
        {
            [Newtonsoft.Json.JsonProperty("tag_name")] public string TagName = null;
            [Newtonsoft.Json.JsonProperty("name")] public string Name = null;
            [Newtonsoft.Json.JsonProperty("html_url")] public string HtmlUrl = null;
            [Newtonsoft.Json.JsonProperty("published_at")] public DateTime? PublishedAt = null;
            [Newtonsoft.Json.JsonProperty("body")] public string Body = null;
            [Newtonsoft.Json.JsonProperty("draft")] public bool Draft = false;
            [Newtonsoft.Json.JsonProperty("prerelease")] public bool Prerelease = false;
            [Newtonsoft.Json.JsonProperty("assets")] public GitHubAsset[] Assets = null;
        }

        private sealed class GitHubAsset
        {
            [Newtonsoft.Json.JsonProperty("name")] public string Name = null;
            [Newtonsoft.Json.JsonProperty("size")] public long Size = 0;
            [Newtonsoft.Json.JsonProperty("browser_download_url")] public string DownloadUrl = null;
            [Newtonsoft.Json.JsonProperty("download_count")] public long DownloadCount = 0;
        }

        private sealed class GitHubError
        {
            [Newtonsoft.Json.JsonProperty("message")] public string Message = null;
        }

        private sealed class ReleaseSnapshot
        {
            public string TagName;
            public string Name;
            public DateTime? PublishedAt;
            public string HtmlUrl;
            public string Body;
            public List<ReleaseAsset> Assets;
        }

        private sealed class ReleaseAsset
        {
            public string Name;
            public long Size;
            public string DownloadUrl;
            public long DownloadCount;
        }
    }
}
