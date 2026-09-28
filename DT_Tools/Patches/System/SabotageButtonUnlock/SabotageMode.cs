namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>破坏按钮额外放行身份档位。Dark=仅黑幕（与原版行为一致，即不给黑方/白板
    /// 额外放行）；关闭功能=原版。与 DoorLockServer.LockDoorMode 形状一致但相互独立——
    /// 客户端显示与房主端校验各自配置。</summary>
    public enum SabotageMode
    {
        Black = 0,
        White = 1,
        All = 2,
        Dark = 3,
    }
}
