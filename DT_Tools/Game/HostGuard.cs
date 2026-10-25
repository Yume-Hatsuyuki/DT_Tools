namespace DT_Tools.Game
{
    public static class HostGuard
    {
        public static bool IsHost => Managers.Host != null && Managers.Host.IsHost;
    }
}
