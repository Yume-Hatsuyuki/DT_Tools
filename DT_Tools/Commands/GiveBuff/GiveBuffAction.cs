namespace DT_Tools.Commands.GiveBuff
{
    /// /givebuff 本次要执行的动作。
    internal enum GiveBuffAction
    {
        /// 添加 BUFF（可带时长，省略用最大时长）。
        Add,
        /// 清除目标的全部 BUFF（第二参为 clear）。
        ClearAll,
        /// 清除目标指定类型 BUFF（第三参为 clear / 0）。
        ClearOne,
    }
}
