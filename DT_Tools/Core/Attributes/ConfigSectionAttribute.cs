using System;

namespace DT_Tools.Core.Attributes
{
    /// 标记基础设施配置段（不属于任何功能，如自动化总开关）。
    /// 段名同样按"类名去后缀（Host/Options/Settings）"推导；类上的 [Config] 字段由引擎绑定。
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigSectionAttribute : Attribute
    {
        /// WebUI 段落分组值：自动化相关段（前端 AUTOMATION 页分流依据）。
        public const string GroupAutomation = "automation";

        /// 段说明（写入 Enabled/字段注释顶部）。
        public string Description { get; }

        /// WebUI 分组（"automation" = 前端 AUTOMATION 页）。引擎在装载期收集进
        /// Engine.GroupOfSection 元数据表，配置层据此分组而不硬编码段名。
        public string Group { get; set; }

        public ConfigSectionAttribute(string description = null)
        {
            Description = description ?? "";
        }
    }
}
