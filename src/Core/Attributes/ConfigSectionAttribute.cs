// 作用：声明配置段的分组信息；不绑定配置项。
using System;

namespace DT_Tools.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigSectionAttribute : Attribute
    {
        public const string GroupAutomation = "automation";

        public string Description { get; }

        public string Group { get; set; }

        public ConfigSectionAttribute(string description = null)
        {
            Description = description ?? "";
        }
    }
}
