using System;
using System.Collections.Generic;
using System.IO;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    internal static class Mp3Playlist
    {
        internal const int MaxNameLength = 40;
        internal const string DefaultListName = "默认歌单";

        internal sealed class Item
        {
            public string Path = "";
            public string Label = "";
            public string Kind = "local";
            public string Alias = "";
            public string Level = "";
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

            public string Mode = "list";

            public string ActiveId = "";
            public string NeteaseLevel = DT_Tools.Game.NeteaseMusic.DefaultLevel;
            public string NeteaseFallbackIp = DT_Tools.Game.NeteaseMusic.DefaultFallbackIp;
            public string NeteaseIpMode = "ipinfo";
            public string NeteaseUserAgent = DT_Tools.Game.NeteaseMusic.DefaultUserAgent;

            public List<ListEntry> Lists = new List<ListEntry>();
        }

        private sealed class LegacyStore
        {
            public List<Item> Items = new List<Item>();
        }

        private static Store _store;
        private static string FullPath => DtPaths.Mp3Playlist;

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
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Log.Warn("Mp3", "歌单文件无法解析，已备份为 playlist.json.broken 并按空歌单处理");
                            File.WriteAllText($"{FullPath}.broken", text);
                        }
                        _store = new Store();
                    }
                    if (_store.Lists == null || _store.Lists.Count == 0)
                    {
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

        internal static ListEntry Active()
        {
            var store = Get();
            return FindIn(store, store.ActiveId) ?? store.Lists[0];
        }

        internal static ListEntry Find(string id)
        {
            return FindIn(Get(), id);
        }

        internal static ListEntry Resolve(string listId)
        {
            return string.IsNullOrEmpty(listId) ? Active() : Find(listId);
        }

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

        internal static bool SelectList(string id)
        {
            var store = Get();
            if (FindIn(store, id) == null)
                return false;
            store.ActiveId = id;
            Save();
            return true;
        }

        internal static int Ensure(string listId, string path) => Ensure(listId, path, InferKind(path), null, null);

        internal static int Ensure(string listId, string path, string kind, string label)
            => Ensure(listId, path, kind, label, null);

        internal static int Ensure(string listId, string path, string kind, string label, string level)
        {
            var list = Resolve(listId);
            if (list == null)
                return -1;
            int index = list.Items.FindIndex(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return index;
            string shown = string.IsNullOrWhiteSpace(label) ? System.IO.Path.GetFileName(path) : label.Trim();
            string k = string.IsNullOrEmpty(kind) ? InferKind(path) : kind;
            string lv = k == "netease" ? NormalizeItemLevel(level) : "";
            list.Items.Add(new Item
            {
                Path = path,
                Label = shown,
                Kind = k,
                Level = lv,
            });
            Save();
            return list.Items.Count - 1;
        }

        internal static bool UpdateItem(string listId, int index, string label, string alias, string newPath, string level, out string error)
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
            var item = list.Items[index];
            string kind = string.IsNullOrEmpty(item.Kind) ? InferKind(item.Path) : item.Kind;

            if (newPath != null)
            {
                string path = newPath.Trim();
                if (path.Length == 0)
                {
                    error = "路径不能为空";
                    return false;
                }
                int clash = list.Items.FindIndex(i =>
                    !ReferenceEquals(i, item) &&
                    string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                if (clash >= 0)
                {
                    error = "歌单里已有相同路径/链接的曲目";
                    return false;
                }
                item.Path = path;
                item.Kind = InferKind(path);
                kind = item.Kind;
            }

            if (label != null)
            {
                string shown = label.Trim();
                if (shown.Length > 0)
                    item.Label = shown.Length > MaxNameLength ? shown.Substring(0, MaxNameLength) : shown;
            }

            if (alias != null)
                item.Alias = NormalizeAlias(alias);

            if (kind == "netease")
            {
                if (level != null)
                    item.Level = NormalizeItemLevel(level);
            }
            else
            {
                item.Level = "";
            }

            Save();
            return true;
        }

        internal static string NormalizeItemLevel(string level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return "";
            string n = DT_Tools.Game.NeteaseMusic.NormalizeLevel(level);
            return n;
        }

        internal static string ResolvePlayLevel(Item item, Store store)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.Level))
                return DT_Tools.Game.NeteaseMusic.NormalizeLevel(item.Level);
            return DT_Tools.Game.NeteaseMusic.NormalizeLevel(store != null ? store.NeteaseLevel : null);
        }

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

        internal const int MaxAliasLength = 60;

        internal static string InferKind(string path)
        {
            string p = (path ?? string.Empty).Trim();
            if (p.StartsWith("netease:", StringComparison.OrdinalIgnoreCase)) return "netease";
            if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "https";
            return "local";
        }

        internal static string NormalizeAlias(string alias)
        {
            string a = (alias ?? string.Empty).Trim();
            return a.Length > MaxAliasLength ? a.Substring(0, MaxAliasLength) : a;
        }

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
                    if (item.Kind == "netease")
                        item.Level = NormalizeItemLevel(item.Level);
                    else
                        item.Level = "";
                }
            }
            if (store.Lists.Count == 0)
                store.Lists.Add(new ListEntry { Id = NewId(), Name = DefaultListName });
            if (FindIn(store, store.ActiveId) == null)
                store.ActiveId = store.Lists[0].Id;
        }

        private static string NewId()
        {
            return $"L{Guid.NewGuid().ToString("N").Substring(0, 10)}";
        }
    }
}
