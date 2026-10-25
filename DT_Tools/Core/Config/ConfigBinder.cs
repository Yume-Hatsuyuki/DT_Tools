using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Core
{
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
                    m.GetParameters()[2].ParameterType.IsGenericParameter &&
                    m.GetParameters()[3].ParameterType == typeof(ConfigDescription));

            var genericBind = bindMethod.MakeGenericMethod(valueType);
            return genericBind.Invoke(config, new[] { section, key, defaultValue, description });
        }

        private static ConfigDescription BuildDescription(ConfigAttribute attr, Type valueType)
        {
            AcceptableValueBase acceptable = null;

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
