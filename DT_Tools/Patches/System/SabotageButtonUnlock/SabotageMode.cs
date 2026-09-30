namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>破坏按钮额外放行身份档位。Dark=仅黑幕（原版行为，默认值，即不给黑方/白方
    /// 额外放行）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。关闭功能=原版。
    /// 与 DoorLockServer.LockDoorMode 形状一致但相互独立——客户端显示与房主端校验各自配置。
    /// 枚举值按档位顺序声明（Dark 最前=下拉首项+默认项），配置存的是成员名，重排不影响旧档。</summary>
    public enum SabotageMode
    {
        Dark = 0,
        Black = 1,
        White = 2,
        All = 3,
    }
}
