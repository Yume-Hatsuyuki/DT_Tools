using System;

namespace DT_Tools.Core.Attributes
{
    /// <summary>
    /// 标记一个自动化模块（Automation/ 下目录级静态类）。
    /// </summary>
    /// <remarks>
    /// 配置段名由引擎按"类名去 Module 后缀"推导；模块需提供 static void Tick(bool hostEnabled)。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AutomationModuleAttribute : Attribute
    {
        /// <summary>
        /// WebUI 卡片标题。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// WebUI 卡片描述。
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 作用面（Client / Host / Both），用于文档与 UI 标签。
        /// </summary>
        public FeatureSide Side { get; }

        /// <summary>
        /// 功能制作者（写入 .cfg 的 Author 行、WebUI 卡片展示）。
        /// </summary>
        /// <remarks>
        /// 缺省按「佚名」署名；
        /// 禁止引入全局作者常量兜底——作者归属逐模块声明，供不同贡献者合并 PR。
        /// </remarks>
        public string Author { get; set; }

        /// <summary>
        /// 功能标签（多选，1–3 个）。
        /// </summary>
        /// <remarks>
        /// 跨领域时同时标多个。词表与说明见 FeatureTag / FeatureTagCatalog。
        /// </remarks>
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
