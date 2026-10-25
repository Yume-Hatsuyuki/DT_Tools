using Protocol;

namespace DT_Tools.Game
{
    public static class LocalPlayer
    {
        public static bool TryGetPlayer(out MyPlayer my, out string error)
        {
            my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
            {
                error = "本机玩家尚未进入对局（大厅/加载中不可用）。";
                return false;
            }
            if (Managers.Network == null || Managers.Network.GameServer == null)
            {
                error = "未连接到 Host（GameServer 链路为空），无法发送 C_ 包。";
                return false;
            }
            error = null;
            return true;
        }

        public static bool IsSurvive => Managers.Game != null && Managers.Game.State == EGameState.Survive;

        public static string StateText => Managers.Game == null ? "(null)" : Managers.Game.State.ToString();

        public static Player FindClientPlayer(int playerId)
        {
            var dict = Managers.Player?.Players;
            return dict != null && dict.TryGetValue(playerId, out var p) ? p : null;
        }
    }
}
