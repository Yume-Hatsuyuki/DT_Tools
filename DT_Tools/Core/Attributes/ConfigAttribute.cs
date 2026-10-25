using System;

namespace DT_Tools.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class ConfigAttribute : Attribute
    {
        public string Description { get; }

        public float Min
        {
            get => _min ?? float.NaN;
            set => _min = value;
        }

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
