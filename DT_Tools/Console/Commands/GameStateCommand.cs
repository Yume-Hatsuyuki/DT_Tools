using System.Text;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /game_state
    ///
    /// 查询当前游戏主状态机 EGameState（大厅 / 选角 / 生存 / 调查 / 学级裁判 / 总结算）；
    /// 处于学级裁判时附加裁判子状态机 ETrialState（讨论 / 投票 / 开票 / 真相公开 / 审判结果）。
    ///
    /// 纯读命令，房主与普通客户端均可执行，数据来源不同：
    ///   - 房主：GameRoom.Instance.State（服务器权威）+ TrialManager.Instance.State，
    ///           并附带 IsTransitioning / IsMigrating 屏障标志；
    ///   - 客户端：Managers.Game.State（由 S_CHANGE_GAME_STATE 同步）
    ///           + TrialMirror.LatestState（由 S_TRIAL_STATE 镜像）。
    ///
    /// 示例:
    ///   /game_state
    ///   /state
    ///   /状态
    /// </summary>
    internal sealed class GameStateCommand : IConsoleCommand
    {
        public string   Name        => "game_state";
        public string[] Aliases     => new[] { "state", "game_phase", "状态", "阶段" };
        public string   Usage       => "game_state";
        public string   Description => "查看当前游戏状态（裁判中含子阶段），房主/客户端均可用。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            bool isHost = Managers.Host != null && Managers.Host.IsHost;

            EGameState state;
            bool transitioning = false;
            bool migrating = false;
            ETrialState? trialState = null;

            if (isHost)
            {
                var room = GameRoom.Instance;
                if (room == null)
                {
                    console.Log("当前没有活动的游戏房间（尚未创建/加入房间）。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"no room\"}");
                    return;
                }

                state = room.State;
                transitioning = room.IsTransitioning;
                migrating = room.IsMigrating;

                // 裁判子状态以服务器 TrialManager 为准
                if (state == EGameState.Trial && TrialManager.Instance != null)
                {
                    trialState = TrialManager.Instance.State;
                }
            }
            else
            {
                if (Managers.Game == null)
                {
                    console.Log("游戏管理器尚未初始化。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"game manager not ready\"}");
                    return;
                }

                state = Managers.Game.State;

                // 客户端无 TrialManager，子状态来自 S_TRIAL_STATE 的镜像缓存
                if (state == EGameState.Trial && TrialMirror.HasState)
                {
                    trialState = TrialMirror.LatestState;
                }
            }

            string stateLabel = StateLabel(state);

            var text = new StringBuilder();
            text.AppendLine("━━━ 当前游戏状态 ━━━");
            text.AppendLine($"  视角:  {(isHost ? "房主（服务器权威）" : "客户端")}");
            text.Append($"  状态:  {state}（{stateLabel}）");

            if (state == EGameState.Trial)
            {
                text.AppendLine();
                if (trialState.HasValue)
                {
                    text.Append($"  裁判子阶段:  {trialState.Value}（{TrialStateLabel(trialState.Value)}）");
                }
                else
                {
                    text.Append("  裁判子阶段:  未知（尚未收到子状态同步）");
                }
            }

            if (isHost && (transitioning || migrating))
            {
                text.AppendLine();
                text.Append($"  屏障:  {(transitioning ? "状态切换中" : "")}{(transitioning && migrating ? " / " : "")}{(migrating ? "主机迁移中" : "")}");
            }

            console.Log(text.ToString(), LogLevel.Info);

            // 结构化 JSON 供 /api/run 脚本使用
            var json = new StringBuilder();
            json.Append("{\"ok\":true")
                .Append(",\"is_host\":").Append(isHost ? "true" : "false")
                .Append(",\"state\":\"").Append(state).Append('"')
                .Append(",\"state_label\":").Append(JsonEscape(stateLabel));

            if (trialState.HasValue)
            {
                json.Append(",\"trial_state\":\"").Append(trialState.Value).Append('"')
                    .Append(",\"trial_state_label\":").Append(JsonEscape(TrialStateLabel(trialState.Value)));
            }

            // 屏障标志仅房主端可见（服务器权威）
            if (isHost)
            {
                json.Append(",\"transitioning\":").Append(transitioning ? "true" : "false")
                    .Append(",\"migrating\":").Append(migrating ? "true" : "false");
            }

            json.Append('}');
            console.SetResult(json.ToString());
        }

        private static string StateLabel(EGameState state)
        {
            switch (state)
            {
                case EGameState.NoneState:     return "未开始";
                case EGameState.Lobby:         return "大厅";
                case EGameState.PickCharacter: return "选择角色";
                case EGameState.Survive:       return "生存阶段";
                case EGameState.Detective:     return "调查阶段";
                case EGameState.Trial:         return "学级裁判";
                case EGameState.TotalResult:   return "总结算";
                default:                       return state.ToString();
            }
        }

        private static string TrialStateLabel(ETrialState state)
        {
            switch (state)
            {
                case ETrialState.Discuss:     return "讨论";
                case ETrialState.VotePhase:   return "投票阶段";
                case ETrialState.VoteResult:  return "开票";
                case ETrialState.Replay:      return "真相公开";
                case ETrialState.TrialResult: return "审判结果";
                default:                      return state.ToString();
            }
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
