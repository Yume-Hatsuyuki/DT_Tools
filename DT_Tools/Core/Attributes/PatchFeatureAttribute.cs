using System;

namespace DT_Tools.Core
{
    /// <summary>
    /// 标记一个功能补丁类。
    /// PatchLoader 扫描此特性：生成 [Section].Enabled，并按 DefaultEnabled 决定是否 Harmony.PatchAll。
    /// 同段子项用 <see cref="ConfigFieldAttribute"/> 声明，由 ConfigBinder 自动 Bind。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PatchFeatureAttribute : Attribute
    {
        /// <summary>配置段名（写入 .cfg 的 [Section]），请保持稳定以免旧配置失效。</summary>
        public string Section { get; }

        /// <summary>Enabled 项的说明（写入 .cfg 注释）。</summary>
        public string Description { get; }

        /// <summary>默认是否启用该功能。</summary>
        public bool DefaultEnabled { get; }

        /// <summary>作用面（Client / Host / Both），用于文档与 UI 标签。</summary>
        public FeatureSide Side { get; }

        /// <summary>作者；有值时在 .cfg 注释顶部生成 Author 行。</summary>
        public string Author { get; }

        /// <param name="section">配置段名</param>
        /// <param name="description">Enabled 中文说明</param>
        /// <param name="defaultEnabled">默认是否启用</param>
        /// <param name="side">作用面</param>
        /// <param name="author">作者</param>
        public PatchFeatureAttribute(
            string section,
            string description,
            bool defaultEnabled = false,
            FeatureSide side = FeatureSide.Client,
            string author = null)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Description = description ?? "";
            DefaultEnabled = defaultEnabled;
            Side = side;
            Author = author;
        }
    }
}
