using System;
using System.Collections.Generic;

namespace DT_Tools.Core
{
    /// <summary>下拉选项：value 是写回配置的原始值，label 是给人看的文字。</summary>
    internal readonly struct ConfigOption
    {
        public readonly string Value;
        public readonly string Label;

        public ConfigOption(string value, string label = null)
        {
            Value = value ?? "";
            Label = string.IsNullOrEmpty(label) ? Value : label;
        }
    }

    /// <summary>
    /// 动态选项注册表。
    ///
    /// 用法（在模块 BindConfig 里，紧跟 config.Bind 之后）：
    ///     OptionProviders.Bind(Section, "CharacterId", CharacterCatalog.Options);
    ///
    /// 之后 ConfigService 导出该配置项时，会自动调用提供者并把结果放进 accepts.options，
    /// 前端只要看到 accepts.options 就渲染下拉——API 层与前端都不再认识任何具体字段名或模块 ID。
    ///
    /// 提供者在每次导出时调用（而非缓存），因此地图/角色表晚于插件加载就绪也没问题；
    /// 数据未就绪时返回空列表即可，前端会自动退回输入框。
    /// </summary>
    internal static class OptionProviders
    {
        private static readonly Dictionary<(string Section, string Key), Func<IReadOnlyList<ConfigOption>>> Map =
            new Dictionary<(string, string), Func<IReadOnlyList<ConfigOption>>>();

        /// <summary>为 (section, key) 绑定选项提供者；重复绑定以后者为准。</summary>
        public static void Bind(string section, string key, Func<IReadOnlyList<ConfigOption>> provider)
        {
            if (string.IsNullOrEmpty(section)) throw new ArgumentException("section required", nameof(section));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key required", nameof(key));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Map[(section, key)] = provider;
        }

        /// <summary>
        /// 取选项。未绑定返回 null（区别于"已绑定但当前为空"的空列表，
        /// 调用方据此决定是否回退到枚举 / AcceptableValues）。
        /// </summary>
        public static IReadOnlyList<ConfigOption> TryGet(string section, string key)
        {
            if (!Map.TryGetValue((section, key), out var provider))
                return null;
            try
            {
                return provider() ?? System.Array.Empty<ConfigOption>();
            }
            catch
            {
                // 游戏数据未就绪等：视为暂无选项，不能让一个提供者拖垮整个配置列表
                return System.Array.Empty<ConfigOption>();
            }
        }
    }
}
