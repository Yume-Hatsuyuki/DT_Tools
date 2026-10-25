using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Core
{
    public static class Engine
    {
        public sealed class LoadResult
        {
            public int MountedCount;
            public int FailedCount;
            public int ModuleCount;
        }

        private static readonly Dictionary<Type, ConfigEntry<bool>> EnabledEntries =
            new Dictionary<Type, ConfigEntry<bool>>();
        private static readonly Dictionary<Type, string> Sections =
            new Dictionary<Type, string>();
        private static readonly Dictionary<string, string> SectionGroups =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, FeatureTag> SectionTags =
            new Dictionary<string, FeatureTag>(StringComparer.Ordinal);
        private static readonly Dictionary<string, FeatureSide> SectionSides =
            new Dictionary<string, FeatureSide>(StringComparer.Ordinal);
        private static readonly List<ModuleInfo> ModuleInfos =
            new List<ModuleInfo>();

        public sealed class ModuleInfo
        {
            public Type Type;
            public string Section;
            public string DisplayName;
            public string Description;
            public FeatureSide Side;
            public string Author;
            public FeatureTag Tags;
            internal Action<bool> Tick;
        }

        public const string EnabledKey = "Enabled";

        public static IReadOnlyList<ModuleInfo> AutomationModules => ModuleInfos;

        public static ConfigFile Config { get; private set; }

        public static LoadResult Load(Harmony harmony, ConfigFile config, ManualLogSource log)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Log.Init(log ?? throw new ArgumentNullException(nameof(log)));

            var result = new LoadResult();
            FeatureLoader.Discover(typeof(Engine).Assembly, out var features, out var modules, out var infra);
            foreach (var m in modules.OrderBy(m => m.Section, StringComparer.Ordinal))
            {
                ModuleInfos.Add(new ModuleInfo
                {
                    Type = m.Type,
                    Section = m.Section,
                    DisplayName = m.DisplayName,
                    Description = m.Description,
                    Side = m.Side,
                    Author = m.Author,
                    Tags = m.Tags,
                    Tick = m.Tick,
                });
            }

            var binds = new List<(string Section, Action Bind)>();
            binds.AddRange(features.Select(f =>
                (f.Section, (Action)(() => BindFeature(config, f)))));
            binds.AddRange(modules.Select(m =>
                (m.Section, (Action)(() => BindModule(config, m)))));
            binds.AddRange(infra.Select(i =>
                (i.Section, (Action)(() =>
                {
                    Sections[i.Type] = i.Section;
                    if (!string.IsNullOrEmpty(i.Group))
                        SectionGroups[i.Section] = i.Group;
                    ConfigBinder.BindFields(config, i.Type, i.Section);
                }))));

            foreach (var bind in binds.OrderBy(x => x.Section, StringComparer.Ordinal))
                bind.Bind();

            foreach (var f in features.OrderBy(x => x.Section, StringComparer.Ordinal))
            {
                try
                {
                    FeatureLoader.Mount(harmony, f);
                    result.MountedCount++;
                    Log.Info(f.Section, $"补丁已挂载（{f.Side}），Enabled={EnabledOf(f.Type)}");
                    try
                    {
                        FeatureLoader.InvokeStaticIfPresent(f.Type, "OnPatched");
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(f.Section, ex, "OnPatched 失败（补丁已挂载，门闩仍生效）");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    Log.Exception(f.Section, ex, "挂载失败（已跳过，其余功能不受影响）");
                }
            }

            foreach (var m in modules)
                Log.Info(m.Section, $"自动化模块就绪：{m.DisplayName}（Enabled={EnabledOf(m.Type)}）");
            result.ModuleCount = ModuleInfos.Count;

            foreach (var type in Sections.Keys.ToArray())
            {
                try
                {
                    FeatureLoader.InvokeStaticIfPresent(type, "OnLoaded");
                }
                catch (Exception ex)
                {
                    Log.Exception(SectionOf(type), ex, "OnLoaded 失败（已跳过，其余功能不受影响）");
                }
            }

            return result;
        }

        public static bool Enabled<T>() => EnabledOf(typeof(T));

        public static bool EnabledOf(Type type)
            => EnabledEntries.TryGetValue(type, out var entry) && entry.Value;

        public static string SectionOf<T>() => SectionOf(typeof(T));

        public static string SectionOf(Type type)
            => Sections.TryGetValue(type, out var section) ? section : type.Name;

        public static string GroupOfSection(string section)
            => section != null && SectionGroups.TryGetValue(section, out var group) ? group : null;

        public static FeatureTag TagsOfSection(string section)
            => section != null && SectionTags.TryGetValue(section, out var tags) ? tags : FeatureTag.None;

        public static FeatureSide? SideOfSection(string section)
            => section != null && SectionSides.TryGetValue(section, out var side) ? side : (FeatureSide?)null;

        public static void TickModules(bool hostEnabled)
        {
            foreach (var module in ModuleInfos)
            {
                try
                {
                    module.Tick(hostEnabled);
                }
                catch (Exception ex)
                {
                    Log.Exception(module.Section, ex, "Tick 失败");
                }
            }
        }

        private static void BindFeature(ConfigFile config, FeatureLoader.FeatureDesc feature)
        {
            var enabled = BindSection(
                config, feature.Type, feature.Section,
                feature.Description, feature.DefaultEnabled, feature.Side, feature.Author);
            SectionTags[feature.Section] = feature.Tags;
            SectionSides[feature.Section] = feature.Side;
            FeatureLoader.WireLifecycle(feature.Type, feature.Section, enabled);
        }

        private static void BindModule(ConfigFile config, FeatureLoader.ModuleDesc module)
        {
            SectionTags[module.Section] = module.Tags;
            SectionSides[module.Section] = module.Side;
            var enabled = BindSection(
                config, module.Type, module.Section,
                module.Description, defaultEnabled: false, module.Side, module.Author);
            FeatureLoader.WireLifecycle(module.Type, module.Section, enabled);
        }

        private static ConfigEntry<bool> BindSection(
            ConfigFile config, Type owner, string section,
            string description, bool defaultEnabled, FeatureSide side, string author)
        {
            var header = $"Author: {author}\nSide: {side}\n{description}".TrimEnd();
            var enabled = config.Bind(section, EnabledKey, defaultEnabled, header);

            EnabledEntries[owner] = enabled;
            Sections[owner] = section;
            ConfigBinder.BindFields(config, owner, section);
            return enabled;
        }
    }
}
