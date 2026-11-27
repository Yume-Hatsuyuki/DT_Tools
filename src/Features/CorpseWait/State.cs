// 作用：维护尸体调查等待的持久状态；不负责补丁挂载与界面渲染。
namespace DT_Tools.Features.CorpseWait
{
    internal static class CorpseWaitState
    {
        public static bool RewritePush;

        public static int PendingWait;
    }
}
