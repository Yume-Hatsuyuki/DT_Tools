namespace DT_Tools.Game
{
    /// <summary>房主判定（唯一实现）：框架命令门禁与各处守卫共用。</summary>
    public static class HostGuard
    {
        public static bool IsHost => Managers.Host != null && Managers.Host.IsHost;
    }
}
