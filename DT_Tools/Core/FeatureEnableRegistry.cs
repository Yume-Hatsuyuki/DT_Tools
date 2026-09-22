using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace DT_Tools.Core
{
    /// <summary>
    /// 登记各 [PatchFeature] 的 Enabled 开关，供运行时门闩与按需 Patch/Unpatch 查询。
    /// </summary>
    internal static class FeatureEnableRegistry
    {
        private static readonly Dictionary<Type, ConfigEntry<bool>> ByType =
            new Dictionary<Type, ConfigEntry<bool>>();

        private static readonly Dictionary<string, ConfigEntry<bool>> BySection =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);

        private static readonly Dictionary<Type, string> SectionByType =
            new Dictionary<Type, string>();

        public static void Register(Type featureType, string section, ConfigEntry<bool> enabled)
        {
            if (featureType == null) throw new ArgumentNullException(nameof(featureType));
            if (string.IsNullOrEmpty(section)) throw new ArgumentException("section required", nameof(section));
            if (enabled == null) throw new ArgumentNullException(nameof(enabled));

            ByType[featureType] = enabled;
            BySection[section] = enabled;
            SectionByType[featureType] = section;
        }

        public static bool IsEnabled(Type featureType)
        {
            if (featureType == null) return false;
            return ByType.TryGetValue(featureType, out var entry) && entry != null && entry.Value;
        }

        public static bool IsEnabled(string section)
        {
            if (string.IsNullOrEmpty(section)) return false;
            return BySection.TryGetValue(section, out var entry) && entry != null && entry.Value;
        }

        public static bool TryGetEntry(Type featureType, out ConfigEntry<bool> entry)
        {
            if (featureType != null && ByType.TryGetValue(featureType, out entry) && entry != null)
                return true;
            entry = null;
            return false;
        }

        public static bool TryGetEntry(string section, out ConfigEntry<bool> entry)
        {
            if (!string.IsNullOrEmpty(section) && BySection.TryGetValue(section, out entry) && entry != null)
                return true;
            entry = null;
            return false;
        }

        public static bool TryGetSection(Type featureType, out string section)
        {
            if (featureType != null && SectionByType.TryGetValue(featureType, out section))
                return true;
            section = null;
            return false;
        }
    }
}
