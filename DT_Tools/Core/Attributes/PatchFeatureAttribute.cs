using System;

namespace DT_Tools.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PatchFeatureAttribute : Attribute
    {
        public string Description { get; }

        public bool DefaultEnabled { get; }

        public FeatureSide Side { get; }

        public string Author { get; set; }

        public FeatureTag Tags { get; set; }

        public PatchFeatureAttribute(
            string description = null,
            bool defaultEnabled = false,
            FeatureSide side = FeatureSide.Client)
        {
            Description = description ?? "";
            DefaultEnabled = defaultEnabled;
            Side = side;
        }
    }
}
