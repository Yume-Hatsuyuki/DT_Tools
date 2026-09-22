using System;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /kill <all|#playerId>
    ///
    /// 对指定/全体存活玩家执行颈环炸弹处决（仅房主、仅 Survive/Detective/Trial 阶段）。
    ///
    /// 非审判阶段额外套用 Louis 念动力瞄准视觉+音效预热，效果更酷炫：
    ///   1. 套 DeadDetective buff（纯语义标记，6 秒后自然过期；CollarBomb 分支不检查它）
    ///   2. 全员广播 ScopeVfx（无视距离，全员可见 Louis 瞄准镜挂在目标身上）
    ///   3. 全员广播 LouisSkillSfx 系统音（无视距离）
    ///   4. 调用原版 Player.OnDeadCollarBomb()：BroadcastWorldVFX(DyingVfx, 896m)
    ///      → PushAfter(6000) → OnDead(null, CollarBomb)
    ///
    /// 审判阶段（Trial）不预热，直接走 OnDeadCollarBomb()，避免干扰审判流程。
    ///
    /// 实现原理：
    ///   - Player.OnDeadCollarBomb() 是原版 GameRoom.GameOver() 全员处决的同一路径，
    ///     命令只是把"GameOver 时全员同时爆炸"的特权开放给单玩家/全体。
    ///   - ScopeVfx 全员广播用 room.Broadcast(new S_PLAY_EFFECT{...}) 绕开
    ///     原版 SendVFX 只发给 Owner 的限制；原版 BroadcastWorldVFX 的 896 距离
    ///     过滤在 OnDeadCollarBomb 内部保留不动（最小化改动）。
    ///
    /// 风险提示：
    ///   - 全员击杀会触发 ExitPlayer 的 TotalResult 自动切换（AliveCount <= 0 → 4 秒后结算）；
    ///   - Trial 阶段杀死 Trial.Black 会导致审判白方胜利时 EndTrial 无法正确处决黑方
    ///     （OnDead 的 IsAlive 守卫拦截二次死亡），命令会输出警告日志；
    ///   - 6 秒延迟内若阶段切换/玩家断线，JobTimer 仍按 tick 触发，
    ///     OnDead 内的 IsAlive 守卫兜底（已死则直接 return）。
    ///
    /// 示例:
    ///   /kill #3
    ///   /kill all
    /// </summary>
    internal sealed class KillCommand : IConsoleCommand
    {
        public string   Name        => "kill";
        public string[] Aliases     => new[] { "处决", "击杀", "execute" };
        public string   Usage       => "kill <all|#playerId>";
        public string   Description => "颈环炸弹处决指定/全体存活玩家（需房主·Survive/Detective/Trial 阶段，6 秒延迟）。";
        public string   Author      => "梦初雪";

        
        public bool RequireHost => true;
public void Execute(string[] args, WebConsole console)
        {
            if (args.Length == 0)
            {
                console.Log("用法: /kill <all|#playerId>\n可用 /list_alive 查看存活玩家列表。", LogLevel.Info);
                return;
            }

            // 房主校验（处决逻辑在 Host 端权威执行）
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（处决逻辑在 Host 端权威执行）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"host only\"}");
                return;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no room\"}");
                return;
            }

            // 屏障校验
            if (room.IsTransitioning)
            {
                console.Log("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"transitioning\"}");
                return;
            }
            if (room.IsMigrating)
            {
                console.Log("正在进行主机迁移，无法执行处决。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"migrating\"}");
                return;
            }

            // 阶段门禁：仅 Survive / Detective / Trial
            if (!IsKillAllowed(room.State))
            {
                console.Log($"处决仅能在生存/调查/裁判阶段使用，当前状态: {room.State}。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid state\",\"state\":\"" + room.State + "\"}");
                return;
            }

            bool isTrial = room.State == EGameState.Trial;
            // 用全限定名避免 Server.Game.Player 与客户端 Player 类型冲突（参考 ListAliveCommand）
            Server.Game.Player trialBlack = (isTrial && room.Trial != null) ? room.Trial.Black : null;

            string targetArg = args[0];

            // === 全员模式 ===
            if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var targets = room.Players
                    .Where(p => p?.PublicInfo != null && p.IsAlive && !p.IsSpectator && !p.IsDummy)
                    .OrderBy(p => p.PublicInfo.PlayerId)
                    .ToList();

                if (targets.Count == 0)
                {
                    console.Log("当前没有存活的非观战玩家。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"no alive players\"}");
                    return;
                }

                // 警告：Trial.Black 在全员名单中
                if (trialBlack != null && targets.Contains(trialBlack))
                {
                    console.Log("⚠ 警告：全员处决名单中包含 Trial.Black，审判白方胜利时将无法正确处决黑方。", LogLevel.Warning);
                }

                // 预热（非审判阶段）：全员瞄准镜 + 警告音
                if (!isTrial)
                {
                    foreach (var player in targets)
                    {
                        // 套 DeadDetective 标记（6 秒后自然过期；CollarBomb 分支不检查它，纯语义）
                        player.BuffComponent.AddBuff(EBuffType.DeadDetective, 6000, isBroadcast: false);
                        // 全员广播 Louis 瞄准镜（无视距离，绕开原版 SendVFX 只发 Owner 的限制）
                        room.Broadcast(new S_PLAY_EFFECT
                        {
                            Type     = EEffectType.ScopeVfx,
                            DeviceId = player.PublicInfo.PlayerId,
                            Pos      = player.PublicInfo.Pos
                        });
                    }
                    // 全员警告音（只播一次，避免叠加刺耳）
                    room.BroadcastSystemSFX(ESoundType.LouisSkillSfx);
                }

                // 执行处决（逐个调用原版路径，6 秒后同时爆炸）
                foreach (var player in targets)
                {
                    player.OnDeadCollarBomb();
                }

                var sb = new StringBuilder();
                sb.AppendLine($"已对全体 {targets.Count} 名存活玩家启动颈环炸弹处决：");
                if (!isTrial)
                {
                    sb.AppendLine("  预热：全员 Louis 瞄准镜 + 警告音已广播");
                    sb.AppendLine("  6 秒后：DyingVfx（896m 内可见）→ 真死亡（CollarBomb）");
                }
                else
                {
                    sb.AppendLine("  审判阶段无预热，6 秒后直接爆炸");
                }
                sb.Append("  ⚠ 全员死亡将触发 4 秒后自动进入总结算。");
                console.Log(sb.ToString(), LogLevel.Message);

                // 结构化 JSON
                var json = new StringBuilder();
                json.Append("{\"ok\":true,\"mode\":\"all\",\"count\":").Append(targets.Count)
                    .Append(",\"is_trial\":").Append(isTrial ? "true" : "false")
                    .Append(",\"targets\":[");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append("{\"pid\":").Append(targets[i].PublicInfo.PlayerId)
                        .Append(",\"name\":").Append(JsonEscape(targets[i].Name ?? ""))
                        .Append('}');
                }
                json.Append("]}");
                console.SetResult(json.ToString());
                return;
            }

            // === 单玩家模式 ===
            int targetId;
            if (targetArg.StartsWith("#") && int.TryParse(targetArg.Substring(1), out targetId))
            {
                // ok
            }
            else if (int.TryParse(targetArg, out targetId))
            {
                // 也允许直接写数字
            }
            else
            {
                console.Log($"无效的目标: {targetArg}（应为 all 或 #数字 / 纯数字）", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid target\"}");
                return;
            }

            var target = room.Players.FirstOrDefault(p => p?.PublicInfo?.PlayerId == targetId);
            if (target == null)
            {
                console.Log($"找不到 PlayerId={targetId} 的玩家，可用 /list_alive 查看存活玩家列表。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target not found\"}");
                return;
            }

            if (target.IsSpectator)
            {
                console.Log($"目标 {target.Name}（#{targetId}）是观战者，无法处决。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target is spectator\"}");
                return;
            }

            if (target.IsDummy)
            {
                console.Log($"目标 {target.Name}（#{targetId}）是 Dummy（主机占位），无法处决。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target is dummy\"}");
                return;
            }

            if (!target.IsAlive)
            {
                console.Log($"目标 {target.Name}（#{targetId}）已死亡，无需处决。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target already dead\"}");
                return;
            }

            // 警告：目标是 Trial.Black
            if (trialBlack != null && target.PublicInfo.PlayerId == trialBlack.PublicInfo.PlayerId)
            {
                console.Log("⚠ 警告：目标是 Trial.Black，审判白方胜利时将无法正确处决黑方。继续执行...", LogLevel.Warning);
            }

            // 预热（非审判阶段）
            if (!isTrial)
            {
                target.BuffComponent.AddBuff(EBuffType.DeadDetective, 6000, isBroadcast: false);
                room.Broadcast(new S_PLAY_EFFECT
                {
                    Type     = EEffectType.ScopeVfx,
                    DeviceId = target.PublicInfo.PlayerId,
                    Pos      = target.PublicInfo.Pos
                });
                room.BroadcastSystemSFX(ESoundType.LouisSkillSfx);
            }

            // 执行处决（原版路径：6 秒 DyingVfx → 真死亡）
            target.OnDeadCollarBomb();

            var msg = new StringBuilder();
            msg.Append($"已对 {target.Name}（#{targetId}）启动颈环炸弹处决：");
            if (!isTrial)
            {
                msg.Append(" 预热（Louis 瞄准镜 + 警告音）→ 6 秒后 DyingVfx（896m 内可见）→ 真死亡（CollarBomb）。");
            }
            else
            {
                msg.Append(" 审判阶段无预热，6 秒后直接爆炸。");
            }
            console.Log(msg.ToString(), LogLevel.Message);

            console.SetResult("{\"ok\":true,\"mode\":\"single\",\"pid\":" + targetId
                + ",\"name\":" + JsonEscape(target.Name ?? "")
                + ",\"is_trial\":" + (isTrial ? "true" : "false") + "}");
        }

        private static bool IsKillAllowed(EGameState state)
        {
            return state == EGameState.Survive
                || state == EGameState.Detective
                || state == EGameState.Trial;
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
