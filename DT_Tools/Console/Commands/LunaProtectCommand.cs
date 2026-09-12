using System;
using System.Linq;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /luna_protect &lt;all|#playerId&gt;
    ///
    /// 让指定玩家（或全体存活玩家）不可被 Black 直接杀害，即套用 Luna 的
    /// Catastrophe 护盾效果（仅房主、仅生存阶段、目标须存活）。
    ///
    /// 目标:
    ///   all           全体存活、非观战玩家（逐人广播）
    ///   #&lt;playerId&gt;  指定玩家数字 ID
    ///
    /// 实现原理：Luna 护盾不是 BUFF，而是客户端 MyPlayer.HasLunaShield 判定：
    /// 亮灯时，Luna 本人或客户端 LunaAbilityIds 名单内的玩家，
    /// Black 出刀会被拦截（播放 RunaShieldSfx，不发送 C_KILL_PLAYER）。
    /// 名单的原版同步方式是 Soi 用 RuleBreaker 偷到 Luna 技能后，
    /// 服务端广播 S_NOTIFY_LUNA_ABILITY。本命令发送同一个包；
    /// 该包每包只携带一个 PlayerId，all 时对每名玩家各广播一包。
    ///
    /// 限制（与原版 Luna 技能完全一致）：
    ///   - 停电（/blackout）期间护盾无效，电力恢复后自动生效；
    ///   - 不防致命诡计（Deadly Trick），只防普通刀杀；
    ///   - 服务端 UseWeapon 无护盾校验，护盾是纯客户端输入门控；
    ///   - 效果持续到本局结束 / 玩家重连，命令之后新加入的玩家需重新施加。
    ///
    /// 示例:
    ///   /luna_protect all
    ///   /luna_protect #3
    /// </summary>
    internal sealed class LunaProtectCommand : IConsoleCommand
    {
        public string   Name        => "luna_protect";
        public string[] Aliases     => new[] { "protect", "护盾", "免死", "保护" };
        public string   Usage       => "luna_protect <all|#playerId>";
        public string   Description => "给指定/全体存活玩家套用 Luna 护盾（亮灯时不可被 Black 刀杀，需房主·生存阶段）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (args.Length == 0)
            {
                console.Log("用法: /luna_protect <all|#playerId>\n可用 /list_alive 查看存活玩家列表。", LogLevel.Info);
                return;
            }

            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（护盾名单由 Host 端广播同步）。", LogLevel.Warning);
                return;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                return;
            }

            if (room.IsTransitioning)
            {
                console.Log("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。", LogLevel.Warning);
                return;
            }

            if (room.IsMigrating)
            {
                console.Log("正在进行主机迁移，无法施加护盾。", LogLevel.Warning);
                return;
            }

            if (room.State != EGameState.Survive)
            {
                console.Log($"护盾命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。", LogLevel.Warning);
                return;
            }

            string targetArg = args[0];

            // all：对每名存活、非观战玩家各广播一包 S_NOTIFY_LUNA_ABILITY
            if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var targets = room.Players
                    .Where(p => p?.PublicInfo != null && p.IsAlive && !p.IsSpectator)
                    .ToList();

                if (targets.Count == 0)
                {
                    console.Log("当前没有存活的非观战玩家。", LogLevel.Warning);
                    return;
                }

                foreach (var player in targets)
                {
                    room.Broadcast(new S_NOTIFY_LUNA_ABILITY
                    {
                        PlayerId = player.PublicInfo.PlayerId
                    });
                }

                console.Log(
                    $"已对全体 {targets.Count} 名存活玩家套用 Luna 护盾：亮灯期间 Black 无法直接刀杀他们。\n" +
                    "注意：与原版 Luna 完全同机制——停电期间护盾无效；不防致命诡计（Deadly Trick）；" +
                    "效果持续到本局结束或玩家重连，期间后加入的玩家需重新施加。",
                    LogLevel.Message);
                return;
            }

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
                return;
            }

            var target = room.Players.FirstOrDefault(p => p?.PublicInfo?.PlayerId == targetId);
            if (target == null)
            {
                console.Log($"找不到 PlayerId={targetId} 的玩家，可用 /list_alive 查看玩家列表。", LogLevel.Warning);
                return;
            }

            if (target.IsSpectator)
            {
                console.Log($"目标 {target.Name}（#{targetId}）是观战者，护盾无意义。", LogLevel.Warning);
                return;
            }

            if (!target.IsAlive)
            {
                console.Log($"目标 {target.Name}（#{targetId}）已死亡，无法套用护盾。", LogLevel.Warning);
                return;
            }

            // Replicator.All：全体客户端（含房主自身客户端）同步 LunaAbilityIds，
            // 与 Soi 使用 RuleBreaker 偷到 Luna 技能时的原版广播完全同包同路径
            room.Broadcast(new S_NOTIFY_LUNA_ABILITY
            {
                PlayerId = targetId
            });

            console.Log(
                $"已对 {target.Name}（#{targetId}）套用 Luna 护盾：亮灯期间 Black 无法直接刀杀该玩家。\n" +
                "注意：与原版 Luna 完全同机制——停电期间护盾无效；不防致命诡计（Deadly Trick）；" +
                "效果持续到本局结束或该玩家重连，期间后加入的玩家需重新施加。",
                LogLevel.Message);
        }
    }
}
