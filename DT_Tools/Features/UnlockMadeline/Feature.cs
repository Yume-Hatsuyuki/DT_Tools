using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.UnlockMadeline
{
    [PatchFeature(
        "梅德琳解锁：解锁梅德琳并修复缺失资源。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysEconomy | FeatureTag.PlayerIdentity | FeatureTag.UiShop,
        Author = "梦初雪")]
    public sealed class UnlockMadelineFeature
    {
        private static void OnEnabled()
        {
            if (Managers.Resource != null && Managers.Resource.IsInit)
                UnlockMadelineUi.InjectAll(Managers.Resource);
        }

        private static void OnDisabled()
        {
            UnlockMadelineUi.Cleanup();
        }
    }
}
