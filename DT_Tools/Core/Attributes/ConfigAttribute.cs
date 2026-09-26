using System;

namespace DT_Tools.Core.Attributes
{
    /// <summary>
    /// 标记功能/模块/基础设施类上的静态配置字段（普通类型，默认值写在字段初始化器里）。
    /// 键名 = 字段名；引擎绑定后把当前值回写字段，配置热改时实时同步。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class ConfigAttribute : Attribute
    {
        /// <summary>.cfg 注释 / WebUI 描述。</summary>
        public string Description { get; }

        /// <summary>可选下界（float/int/double）。</summary>
        public float Min
        {
            get => _min ?? float.NaN;
            set => _min = value;
        }

        /// <summary>可选上界（float/int/double）。</summary>
        public float Max
        {
            get => _max ?? float.NaN;
            set => _max = value;
        }

        public bool HasMin => _min.HasValue;
        public bool HasMax => _max.HasValue;

        private float? _min;
        private float? _max;

        public ConfigAttribute(string description = null)
        {
            Description = description ?? "";
        }
    }
}
