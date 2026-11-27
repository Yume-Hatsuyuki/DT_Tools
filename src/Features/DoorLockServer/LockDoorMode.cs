// 作用：定义锁门额外放行身份的选项枚举；不参与权限判定。
namespace DT_Tools.Features.DoorLockServer
{
    public enum LockDoorMode
    {
        Black = 0,
        White = 1,
        All = 2,
        Dark = 3,
    }
}
