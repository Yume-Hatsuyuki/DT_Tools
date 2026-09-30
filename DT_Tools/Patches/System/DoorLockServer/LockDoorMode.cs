namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>锁门额外放行身份档位。Dark=仅黑幕（与原版行为一致，即不额外放行黑方/白方）；
    /// 关闭功能=原版。与 SabotageButtonUnlock.SabotageMode 形状一致但相互独立。</summary>
    public enum LockDoorMode
    {
        Black = 0,
        White = 1,
        All = 2,
        Dark = 3,
    }
}
