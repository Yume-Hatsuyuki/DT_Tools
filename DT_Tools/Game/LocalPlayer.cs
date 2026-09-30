using Protocol;

namespace DT_Tools.Game
{
    /// <summary>
    /// 本机客户端身份助手（收敛旧 Console/Commands/Weapon/WeaponPacketHelper 的
    /// RequireLocalPlayer / RequireSurvive / FindClientPlayer）。
    /// 使用方：/beacon、/agent、/天匠、/营图、/修电、/拆电 —— 全部为"跟随控制台 / 客户端"
    /// 的本机发包命令，走本机真实网络链路 Managers.Network.GameServer，非房主同样可用。
    /// 校验结果以 (bool, error) 返回，提示文案由调用方经 ctx.Reply/Warn 输出（日志只走命令上下文）。
    /// </summary>
    public static class LocalPlayer
    {
        /// <summary>
        /// 校验本机已进入对局：MyPlayer 已生成（含 PrivateInfo）且到 Host 的 GameServer 链路可用。
        /// 失败时 error 给出对应提示文案，调用方回复后统一返回 "not in game"。
        /// </summary>
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

        /// <summary>
        /// 当前是否生存阶段。武器架交互、递刀、电闸修/拆、任务推进等服务端入口都只在 Survive 生效。
        /// </summary>
        public static bool IsSurvive => Managers.Game != null && Managers.Game.State == EGameState.Survive;

        /// <summary>当前阶段文本（错误提示用；GameManagerEX 尚未就绪时返回 "(null)"）。</summary>
        public static string StateText => Managers.Game == null ? "(null)" : Managers.Game.State.ToString();

        /// <summary>
        /// 从本机客户端玩家表（Managers.Player.Players）查找目标。
        /// 该表只含存活且已 Spawn 的玩家：死亡玩家收到 S_DESPAWN 后被 PlayerManager.Despawn
        /// 移除、旁观者不入表；本机 MyPlayer 不在表内（#自己 查不到，需调用方自行提示）。
        /// 返回客户端 Player（全局命名空间，0.1.15b Player.cs:276 Name/292 PublicInfo/294 TargetPos），
        /// 与 Server.Game.Player 同名，调用方如需服务端玩家请写全名。
        /// </summary>
        public static Player FindClientPlayer(int playerId)
        {
            var dict = Managers.Player?.Players;
            return dict != null && dict.TryGetValue(playerId, out var p) ? p : null;
        }
    }
}
