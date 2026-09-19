using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DT_Tools.Core
{
    /// <summary>
    /// 扫描 [PatchFeature]，按段名排序绑定 Enabled + 子配置，再按开关 PatchAll。
    /// </summary>
    internal static class PatchLoader
    {
        public sealed class LoadResult
        {
            public int EnabledCount { get; set; }
            public int SkippedCount { get; set; }
            public int FailedCount { get; set; }
        }

        private sealed class FeatureDesc
        {
            public Type Type;
            public string Section;
            public string Description;
            public bool DefaultEnabled;
            public string Author;
            public FeatureSide Side;
        }

        public static LoadResult Load(
            Harmony harmony,
            ConfigFile config,
            ManualLogSource log,
            IEnumerable<(string Section, Action BindSection)> extraSections = null)
        {
            var assembly = typeof(PatchLoader).Assembly;
            var features = DiscoverFeatures(assembly);
            var enableEntries = new List<(FeatureDesc Desc, ConfigEntry<bool> Entry)>();
            var sectionBindActions = new List<(string Section, Action Bind)>();

            foreach (var f in features)
            {
                var desc = f;
                sectionBindActions.Add((desc.Section, () =>
                {
                    var entry = BindFeatureSection(config, desc);
                    enableEntries.Add((desc, entry));
                }));
            }

            if (extraSections != null)
            {
                foreach (var extra in extraSections)
                    sectionBindActions.Add((extra.Section, extra.BindSection));
            }

            foreach (var group in sectionBindActions.OrderBy(x => x.Section, StringComparer.Ordinal))
                group.Bind();

            var result = new LoadResult();

            foreach (var (desc, entry) in enableEntries)
            {
                if (!entry.Value)
                {
                    result.SkippedCount++;
                    log.LogInfo($"已跳过功能: {desc.Type.Name} ([{desc.Section}].Enabled = false)");
                    continue;
                }

                try
                {
                    harmony.PatchAll(desc.Type);

                    foreach (var nested in desc.Type.GetNestedTypes(
                                 BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (!nested.IsClass || nested.IsGenericTypeDefinition)
                            continue;
                        if (!Attribute.IsDefined(nested, typeof(HarmonyPatch)))
                            continue;

                        try
                        {
                            harmony.PatchAll(nested);
                        }
                        catch (Exception nestedEx)
                        {
                            result.FailedCount++;
                            log.LogError(
                                $"嵌套补丁加载失败: {desc.Type.Name}.{nested.Name} " +
                                $"([{desc.Section}]) — {nestedEx.GetType().Name}: {nestedEx.Message}");
                            log.LogDebug(nestedEx.ToString());
                        }
                    }

                    DT_Tools.Core.FeatureLogRegistry.Info(desc.Section, "补丁已加载");
                    result.EnabledCount++;
                    log.LogInfo($"已启用功能: {desc.Type.Name} ([{desc.Section}], {desc.Side})");
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    log.LogError(
                        $"功能加载失败: {desc.Type.Name} ([{desc.Section}]) — " +
                        $"{ex.GetType().Name}: {ex.Message}");
                    log.LogDebug(ex.ToString());
                }
            }

            return result;
        }

        private static List<FeatureDesc> DiscoverFeatures(Assembly assembly)
        {
            var list = new List<FeatureDesc>();
            var seenSections = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass)
                    continue;

                var feature = (PatchFeatureAttribute)Attribute.GetCustomAttribute(
                    type, typeof(PatchFeatureAttribute));
                if (feature == null)
                    continue;

                if (!seenSections.Add(feature.Section))
                    throw new InvalidOperationException(
                        $"重复的配置段名 [{feature.Section}]（类型 {type.FullName}）");

                list.Add(new FeatureDesc
                {
                    Type = type,
                    Section = feature.Section,
                    Description = feature.Description,
                    DefaultEnabled = feature.DefaultEnabled,
                    Author = feature.Author,
                    Side = feature.Side
                });
            }

            return list;
        }

        private static ConfigEntry<bool> BindFeatureSection(ConfigFile config, FeatureDesc desc)
        {
            // 写入 .cfg 的 ## 注释元数据（Author / Side），正文为功能说明
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(desc.Author))
                sb.Append("Author: ").Append(desc.Author.Trim()).Append('\n');
            sb.Append("Side: ").Append(FormatSide(desc.Side)).Append('\n');
            if (!string.IsNullOrWhiteSpace(desc.Description))
                sb.Append(desc.Description.Trim());

            var enabled = config.Bind(
                desc.Section,
                "Enabled",
                desc.DefaultEnabled,
                sb.ToString());

            ConfigBinder.BindFields(config, desc.Type, desc.Section);
            return enabled;
        }

        private static string FormatSide(FeatureSide side) => side switch
        {
            FeatureSide.Client => "Client",
            FeatureSide.Host => "Host",
            FeatureSide.Both => "Both",
            _ => side.ToString()
        };
    }
}
