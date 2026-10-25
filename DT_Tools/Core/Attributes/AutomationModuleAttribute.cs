using System;

namespace DT_Tools.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AutomationModuleAttribute : Attribute
    {
        public string DisplayName { get; }

        public string Description { get; }

        public FeatureSide Side { get; }

        public string Author { get; set; }

        public FeatureTag Tags { get; set; }

        public AutomationModuleAttribute(
            string displayName,
            string description = null,
            FeatureSide side = FeatureSide.Client)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Description = description ?? "";
            Side = side;
        }
    }
}
