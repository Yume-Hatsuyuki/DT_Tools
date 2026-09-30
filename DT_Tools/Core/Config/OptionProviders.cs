using System;
using System.Collections.Generic;

namespace DT_Tools.Core
{
    /// <summary>
    /// 动态下拉选项注册表。
    ///
    /// 用法（在功能的 OnLoaded 钩子里）：
    ///     OptionProviders.Bind(Engine.SectionOf&lt;AutoSpawnPointModule&gt;(), "SpawnIndex", AutoSpawnPointLogic.SpawnOptions);
    ///
    /// 配置导出 API 看到提供者就把结果放进 accepts.options，前端据此渲染下拉。
    /// 提供者在每次导出时调用（不缓存），因此地图/角色表晚于插件加载就绪也没问题；
    /// 数据未就绪时返回空列表即可，前端会自动退回输入框。
    /// </summary>
    public static class OptionProviders
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
        /// 调用方据此决定是否回退到枚举 / 数值范围）。
        /// </summary>
        public static IReadOnlyList<ConfigOption> TryGet(string section, string key)
        {
            if (!Map.TryGetValue((section, key), out var provider))
                return null;
            try
            {
                return provider() ?? Array.Empty<ConfigOption>();
            }
            catch (Exception ex)
            {
                // 游戏数据未就绪等：视为暂无选项，一个提供者不能拖垮整个配置列表
                Log.Warn("Core", $"选项提供者 {section}.{key} 失败：{ex.Message}");
                return Array.Empty<ConfigOption>();
            }
        }
    }
}
