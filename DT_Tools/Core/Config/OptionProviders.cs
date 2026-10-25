using System;
using System.Collections.Generic;

namespace DT_Tools.Core
{
    public static class OptionProviders
    {
        private static readonly Dictionary<(string Section, string Key), Func<IReadOnlyList<ConfigOption>>> Map =
            new Dictionary<(string, string), Func<IReadOnlyList<ConfigOption>>>();

        public static void Bind(string section, string key, Func<IReadOnlyList<ConfigOption>> provider)
        {
            if (string.IsNullOrEmpty(section)) throw new ArgumentException("section required", nameof(section));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key required", nameof(key));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Map[(section, key)] = provider;
        }

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
                Log.Warn("Core", $"选项提供者 {section}.{key} 失败：{ex.Message}");
                return Array.Empty<ConfigOption>();
            }
        }
    }
}
