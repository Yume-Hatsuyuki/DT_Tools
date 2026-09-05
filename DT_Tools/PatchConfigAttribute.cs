using System;

namespace DT_Tools
{
    /// <summary>
    /// 标记补丁类，指明对应的配置键名、中文描述、默认启用状态，以及可选的作者
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PatchConfigAttribute : Attribute
    {
        public string ConfigKey { get; }
        public string Description { get; }
        public bool DefaultEnabled { get; }
        public string Author { get; }

        /// <param name="configKey">配置文件中对应的键名，例如 "Enable_Patch_UseEmotionNoCD"</param>
        /// <param name="description">显示在 .cfg 文件注释中的中文说明</param>
        /// <param name="defaultEnabled">该补丁在 .cfg 中的默认启用状态</param>
        /// <param name="author">功能实现者（可选）。有值时会在 .cfg 注释最上方自动生成 "Author: xxx"</param>
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