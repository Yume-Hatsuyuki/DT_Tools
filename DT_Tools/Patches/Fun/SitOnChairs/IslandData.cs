using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 岛屿地图数据（原 IslandMap/data 内嵌版）：把 7 份岛屿数据 JSON/文本
    /// （与原版主图数据逐字段一致，仅 DeviceData 追加 11 条椅子设备）以
    /// EmbeddedResource 方式编译进 DT_Tools.dll，运行时读出为 TextAsset 并
    /// 注入 ResourceManager._resources（0.1.16b ResourceManager.cs:11，私有字典，
    /// 反射写入），键名带 _Island 后缀，与 DataManager.MapKey（DataManager.cs:56）
    /// 拼接规则一致；随后把 LoadMapSet 的地图类型重定向为 Island 即可让游戏
    /// 加载这套数据。功能关闭时不注入，保持完全原版。
    /// </summary>
    internal static class IslandData
    {
        /// <summary>文件名 → 内嵌资源文件名（带命名空间前缀）</summary>
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "MapData",       "MapData_Island.json" },
            { "RoomData",      "RoomData_Island.json" },
            { "DeviceData",    "DeviceData_Island.json" },
            { "Device_DoorData", "Device_DoorData_Island.json" },
            { "TileData",      "TileData_Island.json" },
            { "MissionData",   "MissionData_Island.json" },
            { "MapByte",       "MapByte_Island.txt" }
        };

        private static FieldInfo _resourcesField;

        private static readonly Dictionary<string, TextAsset> Cache = new Dictionary<string, TextAsset>();

        /// <summary>把 7 份岛屿数据注入 ResourceManager._resources，成功返回 true。</summary>
        internal static bool Inject()
        {
            if (_resourcesField == null)
            {
                _resourcesField = typeof(ResourceManager).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_resourcesField == null)
                {
                    Log.Error<SitOnChairsFeature>("[可坐椅子] 找不到 ResourceManager._resources 字段。");
                    return false;
                }
            }

            ResourceManager resource = Managers.Resource;
            if (resource == null)
            {
                return false;
            }
            if (!(_resourcesField.GetValue(resource) is Dictionary<string, UnityEngine.Object> dictionary))
            {
                return false;
            }

            string suffix = "_" + EMapType.Island; // "_Island"

            bool result = true;
            foreach (var file in Files)
            {
                string resourceName = typeof(IslandData).Namespace + ".data." + file.Value;
                if (!Cache.TryGetValue(file.Key, out var value) || value == null)
                {
                    try
                    {
                        using (Stream stream = typeof(IslandData).Assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null)
                            {
                                Log.Error<SitOnChairsFeature>("[可坐椅子] 缺少内嵌数据资源：" + resourceName);
                                result = false;
                                continue;
                            }
                            using (var reader = new StreamReader(stream))
                            {
                                value = new TextAsset(reader.ReadToEnd());
                            }
                        }
                        Cache[file.Key] = value;
                    }
                    catch (Exception ex)
                    {
                        Log.Error<SitOnChairsFeature>("[可坐椅子] 读取内嵌数据 " + file.Value + " 失败：" + ex.Message);
                        result = false;
                        continue;
                    }
                }
                dictionary[file.Key + suffix] = value;
            }
            return result;
        }
    }
}
