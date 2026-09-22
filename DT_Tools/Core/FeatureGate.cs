using System;

namespace DT_Tools.Core
{
    /// <summary>
    /// 运行时功能门闩。补丁入口应优先调用，关闭时放行原版逻辑。
    /// 功能类多为 static class，不能作为泛型实参，请使用 typeof 重载。
    /// </summary>
    internal static class FeatureGate
    {
        /// <summary>指定功能类型当前是否启用。</summary>
        public static bool Enabled(Type featureType) =>
            FeatureEnableRegistry.IsEnabled(featureType);

        /// <summary>按配置段名查询。</summary>
        public static bool Enabled(string section) =>
            FeatureEnableRegistry.IsEnabled(section);
    }
}
