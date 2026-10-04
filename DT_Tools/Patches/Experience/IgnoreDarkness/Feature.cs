using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.IgnoreDarkness
{
    /// <summary>
    /// 断电不影响视野：压制 ApplyDarkness 的灯光切换，并让 CheckValidWithLight 恒通过。
    /// 不改 Managers.Game.Darkness 标志本身（电箱修电提示等仍依赖该标志）。
    /// 热切换会立刻重应用灯光。
    /// </summary>
    [PatchFeature(
        "断电不影响视野：黑暗时保持全局照明与远距可见（名牌/特效/尸体发现距离不受 224 限制）。不解除黑暗交互限制（见「断电可交互」）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class IgnoreDarknessFeature
    {
        private static void OnEnabled() => IgnoreDarknessLogic.ApplyLights();

        private static void OnDisabled() => IgnoreDarknessLogic.ApplyLights();
    }
}
