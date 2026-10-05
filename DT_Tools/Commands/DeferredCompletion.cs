using System;
using System.Threading;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令延迟完成通道（环境量，CommandSession 同款 ThreadStatic 模式）：等待命令结果的
    /// 通道（WebConsole 主线程泵 / RunOnMain）在执行命令前 <see cref="Begin"/> 挂上回填
    /// 回调，命令经 <see cref="CommandContext.Defer"/> 取走回调构造延迟结果并直接 return；
    /// 异步收尾（协程、Steam 回调等仍在主线程的场合）拿到结果后 <see cref="CommandResult.Complete"/>
    /// 回填，等待方随即醒来。ThreadStatic：只有执行命令的主线程携带回调，异步回调触发时
    /// 标记已还原（闭包已捕获，不受影响）。
    /// </summary>
    public static class DeferredCompletion
    {
        [ThreadStatic] private static Action<CommandResult> _current;

        public static Action<CommandResult> Current => _current;

        public static void Begin(Action<CommandResult> sink) => _current = sink;

        public static void End() => _current = null;
    }
}
