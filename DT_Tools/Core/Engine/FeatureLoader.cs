using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Core
{
    /// <summary>
    /// 装载机制：发现（[PatchFeature] / [AutomationModule] / [ConfigSection]）→ 段名推导与查重 →
    /// Harmony 挂载（Feature 同命名空间下所有 [HarmonyPatch] 类，逐个挂载、任一失败即整功能跳过）→
    /// 生命周期接线（OnPatched / OnEnabled / OnDisabled / OnLoaded，静态方法按名约定，全部可选）。
    /// </summary>
    internal static class FeatureLoader
    {
        internal sealed class FeatureDesc
        {
            public Type Type;
            public string Section;
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
            internal Action<bool> Tick;
        }

        internal sealed class InfraDesc
        {
            public Type Type;
            public string Section;

            /// <summary>WebUI 分组（[ConfigSection(Group=…)]，如 "automation"）。</summary>
            public string Group;
        }

        /// <summary>段名推导时去除的类名后缀。</summary>
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

            foreach (var type in SafeGetTypes(assembly))
            {
                if (!type.IsClass)
                    continue;

                var patch = type.GetCustomAttribute<PatchFeatureAttribute>(false);
                if (patch != null)
                {
                    string section = DeriveSection(type);
                    ClaimSection(type, section, claimedSections);
                    if (!featureNamespaces.Add(type.Namespace ?? ""))
                        throw new InvalidOperationException(
                            $"命名空间 {type.Namespace} 中出现第二个 [PatchFeature]（{type.Name}）——目录=功能，一个目录只允许一个 Feature。");
                    features.Add(new FeatureDesc
                    {
                        Type = type,
                        Section = section,
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
                    var tick = FindTick(type);
                    if (tick == null)
                        throw new InvalidOperationException(
                            $"自动化模块 {type.FullName} 缺少 static void Tick(bool hostEnabled)。");
                    modules.Add(new ModuleDesc
                    {
                        Type = type,
                        Section = section,
                        DisplayName = module.DisplayName,
                        Description = module.Description,
                        Side = module.Side,
                        Author = NormalizeAuthor(module.Author),
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

        /// <summary>
        /// GetTypes 的容错版：程序集部分类型加载失败（如可选依赖缺失）时退回已成功的类型，
        /// 避免整机发现阶段崩溃。
        /// </summary>
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

        /// <summary>段名 = 类名去后缀（Feature/Module/Host/Options/Settings）。</summary>
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

        /// <summary>
        /// 挂载一个补丁功能：对 Feature 同命名空间下所有带 [HarmonyPatch] 的类执行 PatchAll。
        /// 自管 Harmony（Feature 声明 static Harmony 字段，如 Transpiler 动态卸载）时跳过自动挂载。
        /// 任一补丁失败即抛出，由 Engine 计入 FailedCount（失败隔离到功能级）。
        /// </summary>
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

            InvokeStaticIfPresent(feature.Type, "OnPatched");
        }

        /// <summary>
        /// Enabled 热切换接线：记录变更日志并回调 OnEnabled/OnDisabled（如实现）。
        /// SettingChanged 在写入值的线程同步触发（WebUI 的 HTTP 线程 / BepInEx 的
        /// .cfg 文件监听线程），而钩子普遍触碰 Unity API（UnlockMadeline 纹理注入、
        /// ChatLimit 场景扫描、NicknameLimit UI 还原等）——统一经 CoroutineHost.Post
        /// 投递到主线程执行，另捕获 on 值防快速连切两次时读到过期状态。
        /// </summary>
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
                        Log.Error(section, $"Enabled 切换回调失败：{ex.GetType().Name}: {ex.Message}");
                    }
                });
            };
        }

        /// <summary>调用类型的无参静态方法（Public/NonPublic），不存在则静默跳过。</summary>
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

        /// <summary>
        /// 作者兜底：逐项声明优先，未声明按「佚名」署名。
        /// 禁止引入全局作者常量（历史教训：全局署名会吞掉不同贡献者的归属）。
        /// </summary>
        private static string NormalizeAuthor(string author)
            => string.IsNullOrWhiteSpace(author) ? "佚名" : author.Trim();
    }
}
