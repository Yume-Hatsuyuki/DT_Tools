using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.BackendProxy
{
    /// <summary>
    /// 出站接口反代（客户端）：把游戏三类 HTTPS 出口改道到用户自建服务器——
    /// 支付/装扮目击后端（Define.PAY_BACKEND_URL、Define.INVENTORY_CHECK_URL）
    /// 与 Discord 反馈上报（BugReporter.UrlFor 的三条 webhook）。
    /// 三个补丁统一同一机制：配置值整替原版 origin，游戏自身路径原样保留
    /// （/pay、/pay-dev、/v1/…、/api/webhooks/…），不做任何 URL 解析与路径约定；
    /// 后端配置只填域名即可同时命中正式/测试两条路径。配置留空 = 直通原版返回值。
    /// 配置热改即时生效，无需重启——消费点每次发包前现读属性
    /// （0.1.16b TiamatPayClient.cs:60、CosmeticSightingReporter.cs:170、BugReporter.cs:92）。
    /// 与「PlaytestMode」同开且已配置时以本功能为准：它的 prefix 先行强制正式服常量，
    /// 本功能 postfix 后行覆盖（Harmony 固定语义：prefix 先于 postfix，不依赖挂载顺序）。
    /// </summary>
    [PatchFeature(
        "出站接口反代：把支付/装扮目击后端与 Discord 反馈上报改道到自建服务器。\n配置值整替原版域名，游戏路径自动保留（/pay、/pay-dev、/v1/…、/api/webhooks/…）；留空 = 游戏原版链接，改完即时生效、无需重启。\n后端地址只填域名（例：https://example.com）；Discord 地址填转发前缀，\n反代需要路径前缀时一并填入（例：https://example.com/discord）。与「测试模式」同开时以本配置为准。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class BackendProxyFeature
    {
        [Config("支付/装扮目击后端的自建域名，整替原版域名 deadlytrick.finalblow.org，游戏路径 /pay、/pay-dev 与 /v1/… 自动保留。留空 = 游戏原版。")]
        public static string PayBaseUrl = "";

        [Config("Discord 反馈上报的转发前缀，整替原版域名 discord.com，游戏路径 /api/webhooks/… 自动保留；反代需要路径前缀时一并填入（如 https://域名/discord）。留空 = 游戏原版。")]
        public static string DiscordBaseUrl = "";
    }
}
