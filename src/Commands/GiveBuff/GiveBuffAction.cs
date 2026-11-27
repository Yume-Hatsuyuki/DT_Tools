// 作用：辅助实现 /GiveBuff 命令；不负责命令注册与其他命令。
namespace DT_Tools.Commands.GiveBuff
{
    internal enum GiveBuffAction
    {
        Add,
        ClearAll,
        ClearOne,
    }
}
