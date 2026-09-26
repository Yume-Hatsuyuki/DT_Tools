using System;

namespace DT_Tools.Core.Attributes
{
    /// <summary>
    /// 标记一个补丁功能（Patches/ 下目录级静态类）。
    /// 配置段名由引擎按"类名去 Feature 后缀"推导，代码零段名字符串；
    /// 同命名空间下所有带 [HarmonyPatch] 的类视为该功能的补丁，由引擎逐个挂载、失败隔离。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PatchFeatureAttribute : Attribute
    {
        /// <summary>Enabled 项的说明（写入 .cfg 注释）。</summary>
        public string Description { get; }

        /// <summary>默认是否启用。</summary>
        public bool DefaultEnabled { get; }

        /// <summary>作用面（Client / Host / Both），用于文档与 UI 标签。</summary>
        public FeatureSide Side { get; }

        /// <summary>
        /// 功能制作者（写入 .cfg 的 Author 行、WebUI 展示）。缺省按「佚名」署名；
        /// 禁止引入全局作者常量兜底——作者归属逐功能声明，供不同贡献者合并 PR。
        /// </summary>
        public string Author { get; set; }

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
