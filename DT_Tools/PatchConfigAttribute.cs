using System;

namespace DT_Tools
{
    /// <summary>
    /// 标记补丁类：配置键、中文描述、默认启用状态、可选作者。
    /// Plugin 启动时扫描带此特性的类型，按配置决定是否 PatchAll。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PatchConfigAttribute : Attribute
    {
        public string ConfigKey { get; }
        public string Description { get; }
        public bool DefaultEnabled { get; }
        public string Author { get; }

        /// <param name="configKey">.cfg 中的键名，例如 Enable_Patch_UseEmotionNoCD</param>
        /// <param name="description">.cfg 注释中的中文说明</param>
        /// <param name="defaultEnabled">默认是否启用</param>
        /// <param name="author">功能实现者；有值时在 .cfg 注释顶部生成 Author 行</param>
        public PatchConfigAttribute(
            string configKey,
            string description,
            bool defaultEnabled = false,
            string author = null)
        {
            ConfigKey = configKey;
            Description = description;
            DefaultEnabled = defaultEnabled;
            Author = author;
        }
    }
}
