using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Core
{
    /// <summary>
    /// 装载门面：Engine.Load 完成发现 → 绑定 → 挂载 → 生命周期接线。
    /// 功能代码只用到三个成员：Enabled&lt;T&gt;()（门闩）、SectionOf&lt;T&gt;()（段名）、TickModules（自动化泵）。
    /// </summary>
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
        private static readonly List<ModuleInfo> ModuleInfos =
            new List<ModuleInfo>();

        /// <summary>自动化模块元数据（WebUI 卡片用；id 即段名；Tick 供 TickModules 泵调用）。</summary>
        public sealed class ModuleInfo
        {
            public Type Type;
            public string Section;
            public string DisplayName;
            public string Description;
            public FeatureSide Side;
            public string Author;
            internal Action<bool> Tick;
        }

        /// <summary>Enabled 键名常量（引擎统一绑定，功能类不声明 Enabled 字段）。</summary>
        public const string EnabledKey = "Enabled";

        public static IReadOnlyList<ModuleInfo> AutomationModules => ModuleInfos;

        public static Harmony Harmony { get; private set; }
        public static ConfigFile Config { get; private set; }

        public static LoadResult Load(Harmony harmony, ConfigFile config, ManualLogSource log)
        {
            Harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Log.Init(log ?? throw new ArgumentNullException(nameof(log)));

            var result = new LoadResult();
            FeatureLoader.Discover(typeof(Engine).Assembly, out var features, out var modules, out var infra);
            foreach (var m in modules)
            {
                ModuleInfos.Add(new ModuleInfo
                {
                    Type = m.Type,
                    Section = m.Section,
                    DisplayName = m.DisplayName,
                    Description = m.Description,
                    Side = m.Side,
                    Author = m.Author,
                    Tick = m.Tick,
                });
            }

            // 配置段统一按字典序绑定，保证 .cfg 段顺序稳定（Enabled 恒为段内第一键）
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

            // OnLoaded：配置绑定完成后调用（OptionProviders 注册等）
            foreach (var type in Sections.Keys.ToArray())
                FeatureLoader.InvokeStaticIfPresent(type, "OnLoaded");

            return result;
        }

        /// <summary>功能/模块门闩：Enabled 配置项由引擎统一绑定，功能类不声明 Enabled 字段。</summary>
        public static bool Enabled<T>() => EnabledOf(typeof(T));

        public static bool EnabledOf(Type type)
            => EnabledEntries.TryGetValue(type, out var entry) && entry.Value;

        /// <summary>段名 = 类名去后缀的推导结果；未注册类型回退类名。</summary>
        public static string SectionOf<T>() => SectionOf(typeof(T));

        public static string SectionOf(Type type)
            => Sections.TryGetValue(type, out var section) ? section : type.Name;

        /// <summary>
        /// 装载期收集的段落分组元数据（[ConfigSection(Group=…)]）；
        /// 未标注的段返回 null（配置层再按功能/模块归属兜底）。
        /// </summary>
        public static string GroupOfSection(string section)
            => section != null && SectionGroups.TryGetValue(section, out var group) ? group : null;

        /// <summary>自动化模块泵：hostEnabled 为总开关值，由 AutomationHost 的主线程 Tick 调用。</summary>
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
                    Log.Error(module.Section, $"Tick 失败：{ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void BindFeature(ConfigFile config, FeatureLoader.FeatureDesc feature)
        {
            var enabled = BindSection(
                config, feature.Type, feature.Section,
                feature.Description, feature.DefaultEnabled, feature.Side, feature.Author);
            FeatureLoader.WireLifecycle(feature.Type, feature.Section, enabled);
        }

        private static void BindModule(ConfigFile config, FeatureLoader.ModuleDesc module)
        {
            var enabled = BindSection(
                config, module.Type, module.Section,
                module.Description, defaultEnabled: false, module.Side, module.Author);
            // 模块与功能同享 Enabled 热切换日志与 OnEnabled/OnDisabled 钩子
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
