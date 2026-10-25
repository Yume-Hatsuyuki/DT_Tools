using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Core
{
    internal static class FeatureLoader
    {
        internal sealed class FeatureDesc
        {
            public Type Type;
            public string Section;

            public FeatureTag Tags;
            public string Description;
            public bool DefaultEnabled;
            public FeatureSide Side;
            public string Author;
        }

        internal sealed class ModuleDesc
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

        internal sealed class InfraDesc
        {
            public Type Type;
            public string Section;

            public string Group;
        }

        private static readonly string[] SectionSuffixes =
            { "Feature", "Module", "Host", "Options", "Settings" };

        public static void Discover(
            Assembly assembly,
            out List<FeatureDesc> features,
            out List<ModuleDesc> modules,
            out List<InfraDesc> infra)
        {
            features = new List<FeatureDesc>();
            modules = new List<ModuleDesc>();
            infra = new List<InfraDesc>();

            var claimedSections = new HashSet<string>(StringComparer.Ordinal);
            var featureNamespaces = new HashSet<string>(StringComparer.Ordinal);
            var moduleNamespaces = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in SafeGetTypes(assembly))
            {
                if (!type.IsClass)
                    continue;

                var patch = type.GetCustomAttribute<PatchFeatureAttribute>(false);
                if (patch != null)
                {
                    string section = DeriveSection(type);
                    FeatureTagCatalog.Validate(patch.Tags, type.FullName);
                    ClaimSection(type, section, claimedSections);
                    if (!featureNamespaces.Add(type.Namespace ?? ""))
                        throw new InvalidOperationException(
                            $"命名空间 {type.Namespace} 中出现第二个 [PatchFeature]（{type.Name}）——目录=功能，一个目录只允许一个 Feature。");
                    features.Add(new FeatureDesc
                    {
                        Type = type,
                        Section = section,
                        Tags = patch.Tags,
                        Description = patch.Description,
                        DefaultEnabled = patch.DefaultEnabled,
                        Side = patch.Side,
                        Author = NormalizeAuthor(patch.Author),
                    });
                    continue;
                }

                var module = type.GetCustomAttribute<AutomationModuleAttribute>(false);
                if (module != null)
                {
                    string section = DeriveSection(type);
                    ClaimSection(type, section, claimedSections);
                    if (!moduleNamespaces.Add(type.Namespace ?? ""))
                        throw new InvalidOperationException(
                            $"命名空间 {type.Namespace} 中出现第二个 [AutomationModule]（{type.Name}）——目录=模块，一个目录只允许一个 Module。");
                    var tick = FindTick(type);
                    if (tick == null)
                        throw new InvalidOperationException(
                            $"自动化模块 {type.FullName} 缺少 static void Tick(bool hostEnabled)。");
                    FeatureTagCatalog.Validate(module.Tags, type.FullName);
                    modules.Add(new ModuleDesc
                    {
                        Type = type,
                        Section = section,
                        DisplayName = module.DisplayName,
                        Description = module.Description,
                        Side = module.Side,
                        Author = NormalizeAuthor(module.Author),
                        Tags = module.Tags,
                        Tick = tick,
                    });
                    continue;
                }

                if (type.GetCustomAttribute<ConfigSectionAttribute>(false) is { } infraAttr)
                {
                    string section = DeriveSection(type);
                    ClaimSection(type, section, claimedSections);
                    infra.Add(new InfraDesc { Type = type, Section = section, Group = infraAttr.Group });
                }
            }
        }

        internal static List<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.Warn("Engine", $"程序集 {assembly.GetName().Name} 有 {ex.Types.Count(t => t == null)} 个类型加载失败，已跳过。");
                return ex.Types.Where(t => t != null).ToList();
            }
        }

        public static string DeriveSection(Type type)
        {
            string name = type.Name;
            foreach (var suffix in SectionSuffixes)
            {
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                    return name.Substring(0, name.Length - suffix.Length);
            }
            return name;
        }

        public static void Mount(Harmony harmony, FeatureDesc feature)
        {
            if (!HasSelfManagedHarmony(feature.Type))
            {
                var patchTypes = SafeGetTypes(feature.Type.Assembly)
                    .Where(t => t.IsClass
                                && t.Namespace == feature.Type.Namespace
                                && Attribute.IsDefined(t, typeof(HarmonyPatch)))
                    .OrderBy(t => t.Name, StringComparer.Ordinal);

                foreach (var patchType in patchTypes)
                {
                    try
                    {
                        harmony.PatchAll(patchType);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(feature.Section, ex, $"补丁 {patchType.Name} 挂载失败");
                        throw;
                    }
                }
            }
        }

        public static void WireLifecycle(Type featureType, string section, ConfigEntry<bool> enabled)
        {
            enabled.SettingChanged += (_, __) =>
            {
                bool on = enabled.Value;
                CoroutineHost.Post(() =>
                {
                    try
                    {
                        Log.Info(section, on ? "Enabled → true（运行时开启）" : "Enabled → false（运行时关闭）");
                        InvokeStaticIfPresent(featureType, on ? "OnEnabled" : "OnDisabled");
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(section, ex, "Enabled 切换回调失败");
                    }
                });
            };
        }

        public static void InvokeStaticIfPresent(Type type, string methodName)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            method?.Invoke(null, null);
        }

        private static Action<bool> FindTick(Type type)
        {
            var method = type.GetMethod(
                "Tick",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            return method == null ? null : (Action<bool>)method.CreateDelegate(typeof(Action<bool>));
        }

        private static bool HasSelfManagedHarmony(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var field in type.GetFields(flags))
            {
                if (typeof(Harmony).IsAssignableFrom(field.FieldType))
                    return true;
            }
            foreach (var property in type.GetProperties(flags))
            {
                if (typeof(Harmony).IsAssignableFrom(property.PropertyType))
                    return true;
            }
            return false;
        }

        private static void ClaimSection(Type owner, string section, HashSet<string> claimed)
        {
            if (!claimed.Add(section))
                throw new InvalidOperationException(
                    $"重复的配置段 [{section}]（{owner.FullName}）——段名由类名自动推导，请检查类命名是否冲突。");
        }

        private static string NormalizeAuthor(string author)
            => string.IsNullOrWhiteSpace(author) ? "佚名" : author.Trim();
    }
}
