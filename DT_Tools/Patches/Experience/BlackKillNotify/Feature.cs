using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// 黑方击杀通报（两端一体单功能，side: Both，范本 SpectatorJoin）：黑方击杀后，
    /// 以原版秘密通话浮文（打字机逐字上屏）向黑幕发送战报——「游戏内时间」换行
    /// 「黑方在【地名】处击杀了【角色】，剩余【次数】攻击次数。」。两个子功能按
    /// 机器角色择一开启：
    /// ClientSide——客户端半：本机黑方击杀被主机接受（RemainKill 下降，该包只发
    /// 攻击者本人）后，模拟触碰一台空闲打字机发送（服务端 Interact 无距离校验，
    /// InteractLock 500ms 自释）。走原版发送接口：与手动打字共用 7 秒秘密通话冷却
    /// （7 秒内连杀的第二条自动跳过），自带自身回声抑制（凶手本人不见浮文）。
    /// ServerSide——房主半：房主检测到黑方击杀（OnDeadMurder，持刀与处决技共用）
    /// 后直发黑方频道，黑方无需安装插件；不占用打字机与冷却，凶手本人也会收到浮文。
    /// 两路同开且两端都装插件时，同一次击杀黑幕会收到两条——建议按部署二选一。
    /// </summary>
    [PatchFeature(
        "黑方击杀通报：黑方击杀后以秘密通话浮文向黑幕发送战报（游戏内时间/击杀地点/角色/剩余攻击次数）。\n" +
        "ClientSide（客户端）：本机黑方模拟触碰空闲打字机发送，需凶手端安装；与手动打字共用 7 秒冷却。\n" +
        "ServerSide（房主）：房主检测到击杀后直发黑方频道，仅需房主安装；不占用打字机与冷却。两路同开会重复上报，建议二选一。",
        defaultEnabled: true,
        side: FeatureSide.Both,
        Author = "梦初雪")]
    public sealed class BlackKillNotifyFeature
    {
        [Config("客户端通报：本机黑方击杀被主机接受后，模拟触碰一台空闲打字机发送战报（需凶手端安装本插件）。")]
        public static bool ClientSide = true;

        [Config("房主通报：房主检测到黑方击杀后直接通知黑幕（仅需房主安装本插件，黑方无需插件；不占用打字机与秘密通话冷却）。")]
        public static bool ServerSide = false;
    }
}
