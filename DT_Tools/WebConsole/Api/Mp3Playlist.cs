using System;
using System.Collections.Generic;
using System.IO;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// 随身MP3 歌单库与播放偏好持久化：路径见 DtPaths.Mp3Playlist。
    /// </summary>
    /// <remarks>
    /// 数据不是配置（无段/键结构，AGENTS.md §9 约束 5 的零字符串约定只约束 [Config] 体系），
    /// 走独立 JSON 文件（Core.Json，Newtonsoft）。所有读写只在 Unity 主线程
    /// （Mp3Api 全部端点经 RunOnMain），无需加锁；每次变更即落盘（文件很小）。
    /// 版本 2 为多歌单：版本 1（单歌单，顶层 items）首次读取时迁移为「默认歌单」并立即落盘。
    /// 同一歌单内按路径去重；不同歌单之间允许同一首歌。文件损坏/缺失即重建默认库。
    /// </remarks>
    internal static class Mp3Playlist
    {
        internal const int MaxNameLength = 40;
        internal const string DefaultListName = "默认歌单";

        internal sealed class Item
        {
            public string Path = "";    // 本地绝对路径或 http(s) 链接
            public string Label = "";   // 展示名（文件名），添加时推导
            public string Kind = "local";   // 曲目类型：local | https | netease
            public string Alias = "";   // 自定义标签（仅显示，不改文件名）
        }

        internal sealed class ListEntry
        {
            public string Id = "";
            public string Name = "";
            public List<Item> Items = new List<Item>();
        }

        internal sealed class Store
        {
            public int Version = 3;
            public float Volume = 0.85f;
            public bool Mic = true;
            public bool Local = true;

            /// <summary>
            /// 播放方式：list=列表循环（默认）| single=单曲循环（重播当前）| random=随机播放。
            /// </summary>
            public string Mode = "list";

            /// <summary>
            /// 界面当前显示的歌单。与「正在播放的歌单」相互独立：浏览别的歌单不会打断播放上下文。
            /// </summary>
            public string ActiveId = "";
            public string NeteaseLevel = DT_Tools.Game.NeteaseMusic.DefaultLevel;
            public string NeteaseFallbackIp = DT_Tools.Game.NeteaseMusic.DefaultFallbackIp;
            public string NeteaseIpMode = "ipinfo";   // ipinfo（默认）| custom
            public string NeteaseUserAgent = DT_Tools.Game.NeteaseMusic.DefaultUserAgent;

            public List<ListEntry> Lists = new List<ListEntry>();
        }

        /// <summary>
        /// 版本 1 的文件结构，仅用于迁移读取。
        /// </summary>
        private sealed class LegacyStore
        {
            public List<Item> Items = new List<Item>();
        }

        private static Store _store;
        private static string FullPath => DtPaths.Mp3Playlist;

        /// <summary>
        /// 惰性加载（首次访问读盘）；损坏/缺失回退默认库，不抛出。
        /// </summary>
        /// <returns>歌单库；首次访问时从磁盘加载，并完成版本迁移与规范化。</returns>
        internal static Store Get()
        {
            if (_store != null)
                return _store;

            bool migrated = false;
            try
            {
                if (File.Exists(FullPath))
                {
                    string text = File.ReadAllText(FullPath);
                    if (Json.TryFrom<Store>(text, out var loaded) && loaded != null)
                    {
                        _store = loaded;
                    }
                    else
                    {
                        // 解析失败：保留原文件供排查，再按空库继续（否则下次保存会覆盖掉它）
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Log.Warn("Mp3", "歌单文件无法解析，已备份为 playlist.json.broken 并按空歌单处理");
                            File.WriteAllText($"{FullPath}.broken", text);
                        }
                        _store = new Store();
                    }
                    if (_store.Lists == null || _store.Lists.Count == 0)
                    {
                        // 版本 1：顶层 items 整体并入「默认歌单」
                        var legacy = Json.TryFrom<LegacyStore>(text, out var old) && old != null ? old : new LegacyStore();
                        _store.Lists = new List<ListEntry>
                        {
                            new ListEntry { Id = NewId(), Name = DefaultListName, Items = legacy.Items ?? new List<Item>() },
                        };
                        migrated = true;
                    }
                }
                else
                {
                    _store = new Store();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Mp3", $"歌单读取失败，按空歌单处理: {ex.Message}");
                _store = new Store();
            }

            Normalize(_store);
            if (migrated)
                Save();
            return _store;
        }

        /// <summary>
        /// 变更后落盘；失败只告警（内存态仍有效，下次变更再试）。
        /// </summary>
        internal static void Save()
        {
            try
            {
                DtPaths.EnsureParentOf(FullPath);
                File.WriteAllText(FullPath, Json.To(Get()));
            }
            catch (Exception ex)
            {
                Log.Warn("Mp3", $"歌单保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 界面当前显示的歌单（ActiveId 无效时回落到第一个）。
        /// </summary>
        /// <returns>当前歌单。</returns>
        internal static ListEntry Active()
        {
            var store = Get();
            return FindIn(store, store.ActiveId) ?? store.Lists[0];
        }

        /// <summary>
        /// 按 id 查歌单。
        /// </summary>
        /// <param name="id">歌单 id。</param>
        /// <returns>找到则返回歌单，否则为 null。</returns>
        internal static ListEntry Find(string id)
        {
            return FindIn(Get(), id);
        }

        /// <summary>
        /// 解析请求里的歌单：listId 为空时取当前歌单，否则按 id 查找。
        /// </summary>
        /// <param name="listId">歌单 id，可为空。</param>
        /// <returns>歌单；指定的 id 不存在时为 null。</returns>
        internal static ListEntry Resolve(string listId)
        {
            return string.IsNullOrEmpty(listId) ? Active() : Find(listId);
        }

        /// <summary>
        /// 新建歌单，并将其设为当前歌单。
        /// </summary>
        /// <param name="rawName">歌单名（会去首尾空白并校验）。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>新歌单；校验失败时为 null。</returns>
        internal static ListEntry CreateList(string rawName, out string error)
        {
            if (!TryNormalizeName(rawName, null, out string name, out error))
                return null;
            var store = Get();
            var entry = new ListEntry { Id = NewId(), Name = name };
            store.Lists.Add(entry);
            store.ActiveId = entry.Id;
            Save();
            return entry;
        }

        /// <summary>
        /// 重命名歌单。
        /// </summary>
        /// <param name="id">歌单 id。</param>
        /// <param name="rawName">新名称（会去首尾空白并校验）。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>重命名成功时为 true。</returns>
        internal static bool RenameList(string id, string rawName, out string error)
        {
            var entry = Find(id);
            if (entry == null)
            {
                error = "歌单不存在";
                return false;
            }
            if (!TryNormalizeName(rawName, id, out string name, out error))
                return false;
            entry.Name = name;
            Save();
            return true;
        }

        /// <summary>
        /// 删除歌单。至少保留一个歌单；删除当前歌单时，当前指向相邻的那一个。
        /// </summary>
        /// <remarks>
        /// 调用方负责：若被删歌单正在播放，先停止播放并清除播放上下文。
        /// </remarks>
        /// <param name="id">歌单 id。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>删除成功时为 true。</returns>
        internal static bool DeleteList(string id, out string error)
        {
            var store = Get();
            var entry = FindIn(store, id);
            if (entry == null)
            {
                error = "歌单不存在";
                return false;
            }
            if (store.Lists.Count <= 1)
            {
                error = "至少保留一个歌单";
                return false;
            }
            int at = store.Lists.IndexOf(entry);
            store.Lists.Remove(entry);
            if (store.ActiveId == id)
                store.ActiveId = store.Lists[Math.Min(at, store.Lists.Count - 1)].Id;
            error = null;
            Save();
            return true;
        }

        /// <summary>
        /// 切换当前显示的歌单。
        /// </summary>
        /// <param name="id">歌单 id。</param>
        /// <returns>切换成功时为 true；歌单不存在时为 false。</returns>
        internal static bool SelectList(string id)
        {
            var store = Get();
            if (FindIn(store, id) == null)
                return false;
            store.ActiveId = id;
            Save();
            return true;
        }

        /// <summary>
        /// 按路径去重入歌单，返回歌单中的下标（已存在则返回原位置）。
        /// </summary>
        /// <param name="listId">歌单 id，为空时取当前歌单。</param>
        /// <param name="path">曲目路径或链接。</param>
        /// <returns>下标；歌单不存在时为 -1。</returns>
        internal static int Ensure(string listId, string path) => Ensure(listId, path, InferKind(path), null);

        /// <summary>
        /// 入指定歌单（同一歌单内按路径去重）。label 为空时以文件名作为展示名。
        /// </summary>
        /// <param name="listId">歌单 id。</param>
        /// <param name="path">曲目路径或链接（网易云为 netease:&lt;id&gt;）。</param>
        /// <param name="kind">曲目类型：local | https | netease。</param>
        /// <param name="label">展示名；为空时取文件名。</param>
        /// <returns>曲目下标；歌单不存在时为 -1。</returns>
        internal static int Ensure(string listId, string path, string kind, string label)
        {
            var list = Resolve(listId);
            if (list == null)
                return -1;
            int index = list.Items.FindIndex(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return index;
            string shown = string.IsNullOrWhiteSpace(label) ? System.IO.Path.GetFileName(path) : label.Trim();
            list.Items.Add(new Item { Path = path, Label = shown, Kind = string.IsNullOrEmpty(kind) ? InferKind(path) : kind });
            Save();
            return list.Items.Count - 1;
        }

        /// <summary>
        /// 设置曲目的自定义标签（只改显示名，不改路径与文件名）。
        /// </summary>
        /// <param name="listId">歌单 id。</param>
        /// <param name="index">曲目下标。</param>
        /// <param name="alias">新标签，空串表示清除。</param>
        /// <param name="error">失败时的中文原因。</param>
        /// <returns>成功为 true。</returns>
        internal static bool SetAlias(string listId, int index, string alias, out string error)
        {
            error = null;
            var list = Resolve(listId);
            if (list == null)
            {
                error = "歌单不存在";
                return false;
            }
            if (index < 0 || index >= list.Items.Count)
            {
                error = "曲目下标无效";
                return false;
            }
            list.Items[index].Alias = NormalizeAlias(alias);
            Save();
            return true;
        }


        /// <summary>
        /// 移除歌单中的一首。
        /// </summary>
        /// <param name="listId">歌单 id，为空时取当前歌单。</param>
        /// <param name="index">歌单中的下标。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>移除成功时为 true。</returns>
        internal static bool RemoveAt(string listId, int index, out string error)
        {
            var list = Resolve(listId);
            if (list == null)
            {
                error = "歌单不存在";
                return false;
            }
            if (index < 0 || index >= list.Items.Count)
            {
                error = "歌单序号越界";
                return false;
            }
            list.Items.RemoveAt(index);
            error = null;
            Save();
            return true;
        }

        /// <summary>
        /// 调整歌单中一首的位置。
        /// </summary>
        /// <param name="listId">歌单 id，为空时取当前歌单。</param>
        /// <param name="from">原下标。</param>
        /// <param name="to">目标下标（移除后再插入到该位置）。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>调整成功时为 true。</returns>
        internal static bool Move(string listId, int from, int to, out string error)
        {
            var list = Resolve(listId);
            if (list == null)
            {
                error = "歌单不存在";
                return false;
            }
            if (from < 0 || from >= list.Items.Count || to < 0 || to >= list.Items.Count)
            {
                error = "歌单序号越界";
                return false;
            }
            var item = list.Items[from];
            list.Items.RemoveAt(from);
            list.Items.Insert(to, item);
            error = null;
            Save();
            return true;
        }

        /// <summary>
        /// 清空歌单（不影响正在播放的曲目）。
        /// </summary>
        /// <param name="listId">歌单 id，为空时取当前歌单。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>清空成功时为 true。</returns>
        internal static bool Clear(string listId, out string error)
        {
            var list = Resolve(listId);
            if (list == null)
            {
                error = "歌单不存在";
                return false;
            }
            list.Items.Clear();
            error = null;
            Save();
            return true;
        }

        /// <summary>
        /// 自然播完后的下一首下标：list=顺序循环，single=重播当前，random=随机且不重复当前。
        /// </summary>
        /// <param name="count">歌单曲目数量。</param>
        /// <param name="mode">播放方式：list | single | random。</param>
        /// <param name="current">当前曲目下标，不在歌单中时为 -1。</param>
        /// <param name="rnd">随机源，仅 random 模式使用。</param>
        /// <returns>下一首的下标；歌单为空时为 -1。</returns>
        internal static int NextIndex(int count, string mode, int current, Random rnd)
        {
            if (count <= 0)
                return -1;
            switch (mode)
            {
                case "single":
                    return current < 0 ? 0 : Math.Min(current, count - 1);
                case "random":
                    if (count == 1)
                        return 0;
                    int pick;
                    do { pick = rnd.Next(count); } while (pick == current);
                    return pick;
                default:
                    return current < 0 ? 0 : (current + 1) % count;
            }
        }

        /// <summary>
        /// 规范化歌单名：去首尾空白，非空、不超过上限、不与其它歌单重名（不区分大小写）。
        /// </summary>
        /// <param name="raw">原始输入。</param>
        /// <param name="exceptId">忽略哪个歌单（重命名时即自身）；新建时为 null。</param>
        /// <param name="name">规范化后的名称。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>校验通过时为 true。</returns>
        internal static bool TryNormalizeName(string raw, string exceptId, out string name, out string error)
        {
            name = (raw ?? "").Trim();
            error = null;
            if (name.Length == 0)
            {
                error = "歌单名不能为空";
                return false;
            }
            if (name.Length > MaxNameLength)
            {
                error = $"歌单名不能超过 {MaxNameLength} 个字符";
                return false;
            }
            foreach (var list in Get().Lists)
            {
                if (list.Id != exceptId && string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    error = "已有同名歌单";
                    return false;
                }
            }
            return true;
        }

        private static ListEntry FindIn(Store store, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return store.Lists.Find(l => l.Id == id);
        }

        /// <summary>自定义标签的最大长度（字符数）。</summary>
        internal const int MaxAliasLength = 60;

        /// <summary>
        /// 由路径推断曲目类型：netease: 前缀为网易云，http(s) 为链接，其余为本地文件。
        /// </summary>
        /// <param name="path">曲目路径或链接。</param>
        /// <returns>曲目类型。</returns>
        internal static string InferKind(string path)
        {
            string p = (path ?? string.Empty).Trim();
            if (p.StartsWith("netease:", StringComparison.OrdinalIgnoreCase)) return "netease";
            if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "https";
            return "local";
        }

        /// <summary>
        /// 规范化自定义标签：去首尾空白，超过 MaxAliasLength 时截断。
        /// </summary>
        /// <param name="alias">用户输入的标签。</param>
        /// <returns>规范化后的标签，可能为空串。</returns>
        internal static string NormalizeAlias(string alias)
        {
            string a = (alias ?? string.Empty).Trim();
            return a.Length > MaxAliasLength ? a.Substring(0, MaxAliasLength) : a;
        }

        /// <summary>
        /// 补齐并修正从磁盘读到的数据：去掉空项、补 id 与默认名、保证至少一个歌单、保证 ActiveId 有效。
        /// </summary>
        /// <param name="store">歌单库。</param>
        private static void Normalize(Store store)
        {
            if (store.Lists == null)
                store.Lists = new List<ListEntry>();
            store.Lists.RemoveAll(l => l == null);
            foreach (var list in store.Lists)
            {
                if (string.IsNullOrEmpty(list.Id))
                    list.Id = NewId();
                if (string.IsNullOrWhiteSpace(list.Name))
                    list.Name = DefaultListName;
                if (list.Items == null)
                    list.Items = new List<Item>();
                list.Items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.Path));
                foreach (var item in list.Items)
                {
                    if (string.IsNullOrEmpty(item.Label))
                        item.Label = System.IO.Path.GetFileName(item.Path);
                        if (string.IsNullOrEmpty(item.Kind))
                            item.Kind = InferKind(item.Path);
                        item.Alias = NormalizeAlias(item.Alias);
                }
            }
            if (store.Lists.Count == 0)
                store.Lists.Add(new ListEntry { Id = NewId(), Name = DefaultListName });
            if (FindIn(store, store.ActiveId) == null)
                store.ActiveId = store.Lists[0].Id;
        }

        /// <summary>
        /// 生成歌单 id（字母 L 加 10 位十六进制，与 JSON 键名无关，仅用于区分）。
        /// </summary>
        /// <returns>新的歌单 id。</returns>
        private static string NewId()
        {
            return $"L{Guid.NewGuid().ToString("N").Substring(0, 10)}";
        }
    }
}
