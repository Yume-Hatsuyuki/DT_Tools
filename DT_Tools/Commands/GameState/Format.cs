using System.Text;
using Protocol;

namespace DT_Tools.Commands.GameState
{
    /// <summary>/game_state 输出格式化：状态中文标签 + 列表文本 + JSON 结果 DTO。</summary>
    internal static class GameStateFormat
    {
        public static string StateLabel(EGameState state)
            => state switch
            {
                EGameState.NoneState     => "未开始",
                EGameState.Lobby         => "大厅",
                EGameState.PickCharacter => "选择角色",
                EGameState.Survive       => "生存阶段",
                EGameState.Detective     => "调查阶段",
                EGameState.Trial         => "学级裁判",
                EGameState.TotalResult   => "总结算",
                _                        => state.ToString(),
            };

        public static string TrialStateLabel(ETrialState state)
            => state switch
            {
                ETrialState.Discuss     => "讨论",
                ETrialState.VotePhase   => "投票阶段",
                ETrialState.VoteResult  => "开票",
                ETrialState.Replay      => "真相公开",
                ETrialState.TrialResult => "审判结果",
                _                       => state.ToString(),
            };

        public static string Reply(GameStateLogic.ReadResult r)
        {
            var text = new StringBuilder();
            text.AppendLine("━━━ 当前游戏状态 ━━━");
            text.AppendLine($"  视角:  {(r.IsHost ? "房主（服务器权威）" : "客户端")}");
            text.Append($"  状态:  {r.State}（{StateLabel(r.State)}）");

            if (r.State == EGameState.Trial)
            {
                text.AppendLine();
                if (r.TrialState.HasValue)
                    text.Append($"  裁判子阶段:  {r.TrialState.Value}（{TrialStateLabel(r.TrialState.Value)}）");
                else
                    text.Append("  裁判子阶段:  未知（尚未收到子状态同步）");
            }

            if (r.IsHost && (r.Transitioning || r.Migrating))
            {
                text.AppendLine();
                text.Append($"  屏障:  {(r.Transitioning ? "状态切换中" : "")}" +
                            $"{(r.Transitioning && r.Migrating ? " / " : "")}{(r.Migrating ? "主机迁移中" : "")}");
            }

            return text.ToString();
        }

        public static object Result(GameStateLogic.ReadResult r)
        {
            // Json.Settings 全局 NullValueHandling.Ignore：
            // 非 trial 时 trial_state/trial_state_label 自动省略，屏障标志仅房主端输出
            return new
            {
                is_host = r.IsHost,
                state = r.State.ToString(),
                state_label = StateLabel(r.State),
                trial_state = r.TrialState?.ToString(),
                trial_state_label = r.TrialState.HasValue ? TrialStateLabel(r.TrialState.Value) : null,
                transitioning = r.IsHost ? r.Transitioning : (bool?)null,
                migrating = r.IsHost ? r.Migrating : (bool?)null,
            };
        }
    }
}
