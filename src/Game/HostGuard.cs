// 作用：判定房主身份；不负责功能开关、配置与补丁挂载。
namespace DT_Tools.Game
{
    public static class HostGuard
    {
        public static bool IsHost => Managers.Host != null && Managers.Host.IsHost;
    }
}
