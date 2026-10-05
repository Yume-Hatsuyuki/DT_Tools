using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// 随身MP3 歌单与播放偏好持久化：BepInEx/config/DT_Tools_Mp3Playlist.json。
    /// 数据不是配置（无段/键结构，AGENTS.md §9 约束 5 的零字符串约定只约束 [Config] 体系），
    /// 走独立 JSON 文件（Core.Json，Newtonsoft）。所有读写只在 Unity 主线程
    /// （Mp3Api 全部端点经 RunOnMain），无需加锁；每次变更即落盘（文件很小）。
    /// 播放即入歌单（按路径去重）；文件损坏/缺失即重建空歌单。
    /// </summary>
    internal static class Mp3Playlist
    {
        private const string FileName = "DT_Tools_Mp3Playlist.json";

        internal sealed class Item
        {
            public string Path = "";    // 本地绝对路径或 http(s) 链接
            public string Label = "";   // 展示名（文件名），添加时推导
        }

        internal sealed class Store
        {
            public float Volume = 0.85f;
            public bool Mic = true;
            public bool Local = true;

            /// <summary>播放方式：list=列表循环（默认）| single=单曲循环（重播当前）| random=随机播放。</summary>
            public string Mode = "list";

            public List<Item> Items = new List<Item>();
        }

        private static Store _store;
        private static string FullPath => Path.Combine(Paths.ConfigPath, FileName);

        /// <summary>惰性加载（首次访问读盘）；损坏/缺失回退默认值，不抛出。</summary>
        internal static Store Get()
        {
            if (_store != null)
                return _store;
            try
            {
                if (File.Exists(FullPath))
                {
                    var loaded = Json.TryFrom<Store>(File.ReadAllText(FullPath), out var s) ? s : null;
                    _store = loaded ?? new Store();
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
            if (_store.Items == null)
                _store.Items = new List<Item>();
            return _store;
        }

        /// <summary>变更后落盘；失败只告警（内存态仍有效，下次变更再试）。</summary>
        internal static void Save()
        {
            try
            {
                File.WriteAllText(FullPath, Json.To(Get()));
            }
            catch (Exception ex)
            {
                Log.Warn("Mp3", $"歌单保存失败: {ex.Message}");
            }
        }

        /// <summary>按路径去重入歌单，返回命中的下标（已存在则返回原位置）。</summary>
        internal static int Ensure(string path)
        {
            var store = Get();
            int index = store.Items.FindIndex(i =>
                string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return index;
            store.Items.Add(new Item
            {
                Path = path,
                Label = System.IO.Path.GetFileName(path),
            });
            Save();
            return store.Items.Count - 1;
        }
    }
}
