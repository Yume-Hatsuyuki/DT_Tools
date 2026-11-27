// 作用：声明断电不影响视野功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.IgnoreDarkness
{
    [PatchFeature(
        "断电不影响视野：黑暗时保持全局照明与远距可见（名牌/特效/尸体发现距离不受 224 限制）。不解除黑暗交互限制（见「断电可交互」）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameDarkness | FeatureTag.GameFuse,
        Author = "梦初雪")]
    public sealed class IgnoreDarknessFeature
    {
        private static void OnEnabled() => IgnoreDarknessLogic.ApplyLights();

        private static void OnDisabled() => IgnoreDarknessLogic.ApplyLights();
    }
}
