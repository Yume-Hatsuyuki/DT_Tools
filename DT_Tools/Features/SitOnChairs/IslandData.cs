using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class IslandData
    {
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

        internal static bool Inject()
        {
            if (_resourcesField == null)
            {
                _resourcesField = typeof(ResourceManager).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_resourcesField == null)
                {
                    Log.Error<SitOnChairsFeature>("找不到 ResourceManager._resources 字段。");
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

            string suffix = $"_{EMapType.Island}";

            bool result = true;
            foreach (var file in Files)
            {
                string resourceName = typeof(IslandData).Namespace + $".data.{file.Value}";
                if (!Cache.TryGetValue(file.Key, out var value) || value == null)
                {
                    try
                    {
                        using (Stream stream = typeof(IslandData).Assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null)
                            {
                                Log.Error<SitOnChairsFeature>($"缺少内嵌数据资源：{resourceName}");
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
                        Log.Error<SitOnChairsFeature>("读取内嵌数据 " + file.Value + $" 失败：{ex.Message}");
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
