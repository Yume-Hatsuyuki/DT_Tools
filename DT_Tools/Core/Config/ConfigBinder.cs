using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Core
{
    /// <summary>
    /// 绑定 [Config] 静态字段：键 = 字段名，默认值 = 字段初始化器当前值；
    /// 绑定后回写字段，配置热改（SettingChanged）时实时同步——功能代码读裸字段即可。
    /// </summary>
    public static class ConfigBinder
    {
        public static void BindFields(ConfigFile config, Type owner, string section)
        {
            var fields = owner
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsStatic && Attribute.IsDefined(f, typeof(ConfigAttribute)))
                .OrderBy(f => f.MetadataToken);

            foreach (var field in fields)
                BindField(config, section, field);
        }

        private static void BindField(ConfigFile config, string section, FieldInfo field)
        {
            if (IsConfigEntry(field.FieldType))
                throw new InvalidOperationException(
                    $"[Config] {field.DeclaringType?.Name}.{field.Name} 必须是普通类型（默认值写在字段初始化器里），不允许 ConfigEntry<T>。");

            var attr = field.GetCustomAttribute<ConfigAttribute>();
            var valueType = field.FieldType;

            object defaultValue = field.GetValue(null);
            if (defaultValue == null && valueType.IsValueType)
                defaultValue = Activator.CreateInstance(valueType);

            var entry = (ConfigEntryBase)InvokeGenericBind(
                config, section, field.Name, defaultValue, BuildDescription(attr, valueType), valueType);

            field.SetValue(null, entry.BoxedValue);
            // SettingChanged 事件在泛型 ConfigEntry<T> 上（ConfigEntryBase 没有），经 EventInfo 订阅
            entry.GetType().GetEvent("SettingChanged")
                ?.AddEventHandler(entry, new EventHandler((_, __) => field.SetValue(null, entry.BoxedValue)));
        }

        private static bool IsConfigEntry(Type type)
            => type == typeof(ConfigEntryBase)
               || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ConfigEntry<>));

        private static object InvokeGenericBind(
            ConfigFile config, string section, string key, object defaultValue,
            ConfigDescription description, Type valueType)
        {
            var bindMethod = typeof(ConfigFile)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m =>
                    m.Name == nameof(ConfigFile.Bind) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 4 &&
                    m.GetParameters()[2].ParameterType.IsGenericParameter);

            var genericBind = bindMethod.MakeGenericMethod(valueType);
            return genericBind.Invoke(config, new[] { section, key, defaultValue, description });
        }

        private static ConfigDescription BuildDescription(ConfigAttribute attr, Type valueType)
        {
            AcceptableValueBase acceptable = null;

            // 注意：不要对枚举使用 AcceptableValueList<T>。BepInEx 的 AcceptableValueList<T> 约束
            // T : IEquatable<T>，Unity/Mono 上对 enum 做 MakeGenericType 会抛 Invalid generic arguments。
            // 枚举选项由配置导出 API 用 Enum.GetNames 填充 accepts。
            if (attr.HasMin || attr.HasMax)
            {
                if (valueType == typeof(float))
                    acceptable = new AcceptableValueRange<float>(
                        attr.HasMin ? attr.Min : float.MinValue,
                        attr.HasMax ? attr.Max : float.MaxValue);
                else if (valueType == typeof(int))
                    acceptable = new AcceptableValueRange<int>(
                        attr.HasMin ? (int)attr.Min : int.MinValue,
                        attr.HasMax ? (int)attr.Max : int.MaxValue);
                else if (valueType == typeof(double))
                    acceptable = new AcceptableValueRange<double>(
                        attr.HasMin ? attr.Min : double.MinValue,
                        attr.HasMax ? attr.Max : double.MaxValue);
            }

            return new ConfigDescription(attr.Description, acceptable);
        }
    }
}
