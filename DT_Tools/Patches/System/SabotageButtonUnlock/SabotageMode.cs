namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>破坏按钮额外放行身份（黑幕原本就可用，维持不变；关闭功能=原版）。
    /// 与 DoorLockServer.LockDoorMode 形状一致但相互独立——客户端显示与房主端校验各自配置。</summary>
    public enum SabotageMode
    {
        Black = 0,
        White = 1,
        All = 2,
    }
}
