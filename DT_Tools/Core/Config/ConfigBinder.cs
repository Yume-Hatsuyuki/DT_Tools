using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace DT_Tools.Core
{
    /// <summary>
    /// 扫描带 <see cref="PatchFeatureAttribute"/> 的类型，
    /// 将其上标记了 <see cref="ConfigFieldAttribute"/> 的静态 ConfigEntry 字段自动 Bind。
    /// Enabled 由 <see cref="Patching.PatchLoader"/> 负责，保证段内顺序：Enabled 在前，子项在后。
    /// </summary>
    internal static class ConfigBinder
    {
        /// <summary>
        /// 为指定功能类型绑定所有 [ConfigField] 字段到同一 Section。
        /// 调用前该 Section 的 Enabled 应已 Bind（由 PatchLoader 保证）。
        /// </summary>
        public static void BindFields(ConfigFile config, Type featureType, string section)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (featureType == null) throw new ArgumentNullException(nameof(featureType));
            if (string.IsNullOrEmpty(section)) throw new ArgumentException("section required", nameof(section));

            var fields = featureType
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsStatic && Attribute.IsDefined(f, typeof(ConfigFieldAttribute)))
                .OrderBy(f => f.MetadataToken); // 源码声明顺序，稳定

            foreach (var field in fields)
            {
                var attr = (ConfigFieldAttribute)Attribute.GetCustomAttribute(field, typeof(ConfigFieldAttribute));
                var entryType = field.FieldType;

                if (!entryType.IsGenericType ||
                    entryType.GetGenericTypeDefinition() != typeof(ConfigEntry<>))
                {
                    throw new InvalidOperationException(
                        $"[ConfigField] {featureType.Name}.{field.Name} 必须是 ConfigEntry<T>，实际为 {entryType.Name}");
                }

                var valueType = entryType.GetGenericArguments()[0];
                string key = string.IsNullOrEmpty(attr.Key) ? field.Name : attr.Key;

                object defaultValue = CoerceDefault(attr.Default, valueType);
                var description = BuildDescription(attr, valueType);

                // ConfigFile.Bind<T>(section, key, default, description)
                var bindMethod = typeof(ConfigFile)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m =>
                        m.Name == nameof(ConfigFile.Bind) &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 4 &&
                        m.GetParameters()[2].ParameterType.IsGenericParameter);

                var genericBind = bindMethod.MakeGenericMethod(valueType);
                object entry = genericBind.Invoke(config, new object[]
                {
                    section,
                    key,
                    defaultValue,
                    description
                });

                field.SetValue(null, entry);
            }
        }

        private static object CoerceDefault(object value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            if (targetType.IsInstanceOfType(value))
                return value;

            // Attribute 里常写 0.3f / 1 / true；统一转换
            try
            {
                if (targetType.IsEnum)
                    return Enum.ToObject(targetType, value);

                return Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法将默认值 {value} ({value.GetType().Name}) 转为 {targetType.Name}", ex);
            }
        }

        private static ConfigDescription BuildDescription(ConfigFieldAttribute attr, Type valueType)
        {
            AcceptableValueBase acceptable = null;

            if (attr.HasMin || attr.HasMax)
            {
                // 仅对 float/double/int 等常见数值建 Range；其余忽略 Min/Max
                if (valueType == typeof(float))
                {
                    float min = attr.HasMin ? attr.Min : float.MinValue;
                    float max = attr.HasMax ? attr.Max : float.MaxValue;
                    acceptable = new AcceptableValueRange<float>(min, max);
                }
                else if (valueType == typeof(int))
                {
                    int min = attr.HasMin ? (int)attr.Min : int.MinValue;
                    int max = attr.HasMax ? (int)attr.Max : int.MaxValue;
                    acceptable = new AcceptableValueRange<int>(min, max);
                }
                else if (valueType == typeof(double))
                {
                    double min = attr.HasMin ? attr.Min : double.MinValue;
                    double max = attr.HasMax ? attr.Max : double.MaxValue;
                    acceptable = new AcceptableValueRange<double>(min, max);
                }
            }

            return new ConfigDescription(attr.Description ?? "", acceptable);
        }
    }
}
