namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>锁门额外放行身份（黑幕原本就可锁门，维持不变；关闭功能=原版）。</summary>
    public enum LockDoorMode
    {
        Black = 0,
        White = 1,
        All = 2,
    }
}
