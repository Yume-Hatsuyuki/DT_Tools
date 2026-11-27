// 作用：声明梅德琳解锁功能、配置项与启用状态；不实现补丁与运行时逻辑。
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
