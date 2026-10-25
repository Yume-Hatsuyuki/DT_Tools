using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.AlwaysShowPing
{
    [PatchFeature(
        "延迟常显：离开大厅后仍在右下角实时显示网络延迟（原版仅大厅可见）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiHud | FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class AlwaysShowPingFeature
    {
        [Config("刷新间隔（秒）。原版为 1 秒。", Min = 0.2f, Max = 5f)]
        public static float RefreshInterval = 1f;

        private static void OnDisabled() => AlwaysShowPingLogic.RestoreParent();
    }
}
