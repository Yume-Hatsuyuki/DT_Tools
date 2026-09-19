using System;

namespace DT_Tools.Core
{
    /// <summary>
    /// 标记自动化模块类。由 AutomationRegistry 发现；不进入 PatchLoader。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AutomationModuleAttribute : Attribute
    {
        public string Id { get; }
        public string Section { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public FeatureSide Side { get; }
        public string Author { get; }

        public AutomationModuleAttribute(
            string id,
            string section,
            string displayName,
            string description,
            FeatureSide side = FeatureSide.Client,
            string author = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Section = section ?? throw new ArgumentNullException(nameof(section));
            DisplayName = displayName ?? id;
            Description = description ?? "";
            Side = side;
            Author = author;
        }
    }
}
