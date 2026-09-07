using System;

namespace DT_Tools
{
    /// <summary>
    /// 标记补丁类：配置段名、中文描述、默认启用状态、可选作者。
    /// Plugin 启动时扫描带此特性的类型，按配置决定是否 PatchAll。
    /// 开关与子项写在同一段内，Enabled 始终为该段第一个键。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PatchConfigAttribute : Attribute
    {
        /// <summary>配置段名，与源码成员同名，例如 CreateLobby、GetTargetPlayer。</summary>
        public string Section { get; }

        /// <summary>.cfg 中 Enabled 的注释说明。</summary>
        public string Description { get; }

        /// <summary>默认是否启用。</summary>
        public bool DefaultEnabled { get; }

        /// <summary>功能实现者；有值时在 .cfg 注释顶部生成 Author 行。</summary>
        public string Author { get; }

        /// <param name="section">配置段名，与源码成员同名</param>
        /// <param name="description">.cfg 中 Enabled 的中文说明</param>
        /// <param name="defaultEnabled">默认是否启用</param>
        /// <param name="author">功能实现者；有值时在注释顶部生成 Author 行</param>
        public PatchConfigAttribute(
            string section,
            string description,
            bool defaultEnabled = false,
            string author = null)
        {
            Section = section;
            Description = description;
            DefaultEnabled = defaultEnabled;
            Author = author;
        }
    }
}
