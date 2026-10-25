using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DarkRadar
{
    [PatchFeature(
        "黑幕情报共享：白方/持刀者在平板地图上也能看到凶器刷新位置和可破坏电闸标记（原版仅黑幕 Dark 可见）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameIntel | FeatureTag.UiMapPin | FeatureTag.PlayerIdentity,
        Author = "梦初雪")]
    public sealed class DarkRadarFeature
    {
        [Config("巡检间隔（秒）。", Min = 0.5f, Max = 30f)]
        public static float CheckInterval = 2f;

        private static void OnEnabled() => DarkRadarState.ResetClock();

        private static void OnDisabled() => DarkRadarLogic.ClearAll();
    }
}
