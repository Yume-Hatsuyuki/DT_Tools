namespace DT_Tools.Commands.GiveBuff
{
    /// <summary>/givebuff 本次要执行的动作。</summary>
    internal enum GiveBuffAction
    {
        /// <summary>添加 BUFF（可带时长，省略用最大时长）。</summary>
        Add,
        /// <summary>清除目标的全部 BUFF（第二参为 clear）。</summary>
        ClearAll,
        /// <summary>清除目标指定类型 BUFF（第三参为 clear / 0）。</summary>
        ClearOne,
    }
}
