using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.BackendProxy
{
    [PatchFeature(
        "出站接口反代：把装扮目击/库存校验后端与 Discord 反馈上报改道到自建服务器。\n配置值整替原版域名，游戏路径自动保留（/pay、/pay-dev、/v1/…、/api/webhooks/…）；留空 = 游戏原版链接，改完即时生效、无需重启。\n后端地址只填域名（例：https://example.com）；Discord 地址填转发前缀，\n反代需要路径前缀时一并填入（例：https://example.com/discord）。与「测试模式」同开时以本配置为准。\n注：免费货币两个版本都经 Steam 掉落，不走反代支付 grant 接口。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysBackend,
        Author = "梦初雪")]
    public sealed class BackendProxyFeature
    {
        [Config("装扮目击/库存校验后端的自建域名，整替原版域名 deadlytrick.finalblow.org，游戏路径 /pay、/pay-dev 与 /v1/… 自动保留。留空 = 游戏原版。")]
        public static string PayBaseUrl = "";

        [Config("Discord 反馈上报的转发前缀，整替原版域名 discord.com，游戏路径 /api/webhooks/… 自动保留；反代需要路径前缀时一并填入（如 https://域名/discord）。留空 = 游戏原版。")]
        public static string DiscordBaseUrl = "";
    }
}
