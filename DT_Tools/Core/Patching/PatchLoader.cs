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
    /// 扫描 [PatchFeature]，绑定 Enabled + 子配置，并尝试 Harmony.PatchAll。
    /// Enabled 表示运行时是否执行逻辑（门闩）；补丁默认全部挂载，失败按功能隔离。
    /// 具备 OnPatched/OnEnabled/OnDisabled 静态方法的功能可响应热切换（如 Transpiler Unpatch）。
    /// </summary>
    internal static class PatchLoader
    {
        public sealed class LoadResult
        {
            public int MountedCount { get; set; }
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
                try
                {
                    MountFeature(harmony, desc, log);
                    result.MountedCount++;
                    log.LogInfo($"已挂载功能: {desc.Type.Name} ([{desc.Section}], {desc.Side}, Enabled={entry.Value})");
                    FeatureLogRegistry.Info(
                        desc.Section,
                        $"补丁已挂载（{desc.Side}），当前 Enabled={entry.Value}");

                    WireLifecycle(desc.Type, entry, log, desc.Section);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    log.LogError(
                        $"功能挂载失败: {desc.Type.Name} ([{desc.Section}]) — " +
                        $"{ex.GetType().Name}: {ex.Message}");
                    log.LogDebug(ex.ToString());
                    FeatureLogRegistry.Error(
                        desc.Section,
                        $"挂载失败: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return result;
        }

        private static void MountFeature(Harmony harmony, FeatureDesc desc, ManualLogSource log)
        {
            if (HasSelfManagedHarmony(desc.Type))
            {
                InvokeLifecycleStatic(desc.Type, FeatureLifecycleNames.OnPatched, log);
                return;
            }

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
                    log.LogError(
                        $"嵌套补丁加载失败: {desc.Type.Name}.{nested.Name} " +
                        $"([{desc.Section}]) — {nestedEx.GetType().Name}: {nestedEx.Message}");
                    log.LogDebug(nestedEx.ToString());
                    throw;
                }
            }

            if (HasLifecycleMethods(desc.Type))
                InvokeLifecycleStatic(desc.Type, FeatureLifecycleNames.OnPatched, log);
        }

        private static bool HasSelfManagedHarmony(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var f in type.GetFields(flags))
            {
                if (typeof(Harmony).IsAssignableFrom(f.FieldType))
                    return true;
            }
            foreach (var p in type.GetProperties(flags))
            {
                if (typeof(Harmony).IsAssignableFrom(p.PropertyType))
                    return true;
            }
            return false;
        }

        private static bool HasLifecycleMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            return type.GetMethod(FeatureLifecycleNames.OnPatched, flags) != null
                || type.GetMethod(FeatureLifecycleNames.OnEnabled, flags) != null
                || type.GetMethod(FeatureLifecycleNames.OnDisabled, flags) != null;
        }

        private static void WireLifecycle(
            Type featureType, ConfigEntry<bool> entry, ManualLogSource log, string section)
        {
            // 始终记录 Enabled 热切换；有生命周期方法则一并调用（缺方法静默跳过）
            entry.SettingChanged += (_, __) =>
            {
                try
                {
                    if (entry.Value)
                    {
                        FeatureLogRegistry.Info(section, "Enabled → true（运行时开启）");
                        InvokeLifecycleStatic(featureType, FeatureLifecycleNames.OnEnabled, log);
                    }
                    else
                    {
                        FeatureLogRegistry.Info(section, "Enabled → false（运行时关闭）");
                        InvokeLifecycleStatic(featureType, FeatureLifecycleNames.OnDisabled, log);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(
                        $"[Lifecycle] {featureType.Name} Enabled={entry.Value} 回调失败: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                    log.LogDebug(ex.ToString());
                    FeatureLogRegistry.Error(
                        section,
                        $"Enabled 切换回调失败: {ex.GetType().Name}: {ex.Message}");
                }
            };
        }

        private static void InvokeLifecycleStatic(Type featureType, string methodName, ManualLogSource log)
        {
            var method = featureType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            // 可选钩子：未实现的方法不警告（例如仅有 OnDisabled 而无 OnPatched）
            if (method == null)
                return;
            method.Invoke(null, null);
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

            FeatureEnableRegistry.Register(desc.Type, desc.Section, enabled);
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
