// 作用：声明房间内切换隐藏身份立即生效的功能开关与启停回调；不实现补丁拦截与网络同步。
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.HideIdentityLive
{
    [PatchFeature(
        "房间内切换隐藏身份立即生效：房主直接更新名册并广播，客户端复用原生进房包请求更新；关闭时回到仅下次进房生效。需房主与切换方均启用。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class HideIdentityLiveFeature
    {
        public static void OnPatched()
        {
            if (Engine.Enabled<HideIdentityLiveFeature>())
                HideIdentitySettingUi.Refresh();
        }

        public static void OnEnabled()
        {
            HideIdentitySettingUi.Refresh();
        }

        public static void OnDisabled()
        {
            HideIdentitySettingUi.Refresh();
        }
    }
}
