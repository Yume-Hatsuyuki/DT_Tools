using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /givebuff &lt;all|#id&gt; &lt;buff|clear&gt; [seconds|clear|0]
    ///
    /// 不带参数时打印可用 BUFF 列表，不执行添加。
    ///
    /// 目标:
    ///   all          全体玩家
    ///   #&lt;playerId&gt;  指定玩家数字 ID
    ///
    /// 清除:
    ///   /givebuff all clear              → 清除全体全部 BUFF
    ///   /givebuff #1 clear               → 清除指定玩家全部 BUFF
    ///   /givebuff #1 footprint clear     → 清除指定玩家的 Footprint
    ///   /givebuff #1 footprint 0         → 同上（时长 0 = 清除）
    ///
    /// 添加:
    ///   /givebuff #1 footprint 3         → 添加 Footprint，持续 3 秒
    ///   /givebuff #1 footprint           → 省略秒数，使用最大时长 3600 秒
    ///
    /// 说明:
    ///   - 添加走 BuffComponent.AddBuff；若已有同类 BUFF 会先 RemoveBuffForce 再添加（刷新时长）
    ///   - 时长上限为 3600 秒（与原版 Raasrush/DetailCheck/MindControl 等被动技能一致）；
    ///     超出会自动钳制到 3600 秒；省略秒数则默认使用 3600 秒
    ///   - 原版 Flush 只遍历 AlivePlayers，大厅列表为空导致不会自动到期；
    ///     本命令在添加后额外 PushAfter(durationMs) 调用 RemoveBuffForce 作为兜底
    ///   - 清除走 RemoveBuffForce / Clear，不依赖 AlivePlayers，大厅也可用
    ///
    /// 示例:
    ///   /givebuff all scanup             ← 省略秒数，使用最大 3600 秒
    ///   /givebuff all scanup 3600        ← 显式指定上限
    ///   /givebuff #5 slow 5
    ///   /givebuff #1 footprint 0
    ///   /givebuff all clear
    /// </summary>
    internal sealed class GiveBuffCommand : IConsoleCommand
    {
        public string Name => "givebuff";
        public string[] Aliases => new[] { "buff" };
        public string Usage => "givebuff <all|#id> <buff|clear> [seconds|clear|0]";
        public string Description => "给所有/指定玩家添加或清除 BUFF。不带参数时显示可用 BUFF 列表。";
        public string Author => "梦初雪";

        /// <summary>
        /// 单次添加 BUFF 的最大时长（秒）。与原版 Raasrush/DetailCheck/MindControl
        /// 等被动技能挂载时长一致（3600000 ms）。
        /// </summary>
        private const int MaxDurationSeconds = 3600;

        private static readonly Dictionary<string, EBuffType> BuffAliases =
            new Dictionary<string, EBuffType>(StringComparer.OrdinalIgnoreCase)
        {
            { "slow",           EBuffType.Slow },
            { "stun",           EBuffType.Stun },
            { "stop",           EBuffType.Stop },
            { "exhausted",      EBuffType.Exhausted },
            { "tired",          EBuffType.Exhausted },
            { "potioncorrect",  EBuffType.PotionCorrect },
            { "potion_correct", EBuffType.PotionCorrect },
            { "potionwrong",    EBuffType.PotionWrong },
            { "potion_wrong",   EBuffType.PotionWrong },
            { "staminaup",      EBuffType.StaminaUp },
            { "stamina",        EBuffType.StaminaUp },
            { "scanup",         EBuffType.ScanUp },
            { "scan",           EBuffType.ScanUp },
            { "quantumleap",    EBuffType.QuantumLeap },
            { "quantum",        EBuffType.QuantumLeap },
            { "deaddetective",  EBuffType.DeadDetective },
            { "dead_detective", EBuffType.DeadDetective },
            { "theworld",       EBuffType.TheWorld },
            { "world",          EBuffType.TheWorld },
            { "despair",        EBuffType.Despair },
            { "despairimmune",  EBuffType.DespairImmune },
            { "despair_immune", EBuffType.DespairImmune },
            { "despairguard",   EBuffType.DespairGuard },
            { "despair_guard",  EBuffType.DespairGuard },
            { "grouppanelty",   EBuffType.GroupPanelty },
            { "group_panelty",  EBuffType.GroupPanelty },
            { "footprint",      EBuffType.Footprint },
        };

        private static readonly string BuffList =
            "━━━ 可用 BUFF（对照 Protocol.EBuffType 全量收录） ━━━\n" +
            " 【移动/控制】\n" +
            "  slow             减速（原版：卡门/被玩具武器击中触发）\n" +
            "  stun             眩晕，锁定操作\n" +
            "  stop             定身，完全锁定操作\n" +
            "  exhausted        体力耗尽，减速\n" +
            " 【道具效果】\n" +
            "  potioncorrect    正确注射，加速\n" +
            "  potionwrong      错误注射，异常状态\n" +
            " 【角色技能（隐藏，原版不广播）】\n" +
            "  staminaup        体力提升（Hasung 技能 Raasrush）\n" +
            "  scanup           扫描加速（Miyuki 技能 DetailCheck，读条 2s→0.5s）\n" +
            "  quantumleap      量子跃迁（Jeremy 技能 MindControl）\n" +
            "  deaddetective    死亡广播（Louis 技能 Telekinesis）\n" +
            " 【场景/系统】\n" +
            "  theworld         时间停止表现（Seol 技能 TimeStop，客户端表现完整）\n" +
            "  footprint        留下可追踪脚印\n" +
            " 【绝望值系统（服务端未见触发逻辑，实装状态未知）】\n" +
            "  despair          绝望状态\n" +
            "  despairimmune    绝望免疫\n" +
            "  despairguard     绝望守护\n" +
            "  grouppanelty     群体惩罚（RefreshGroupPanelty 当前为空实现）\n" +
            "【清除】\n" +
            "  /givebuff all clear\n" +
            "  /givebuff #1 clear\n" +
            "  /givebuff #1 footprint 0\n" +
            "  /givebuff #1 footprint clear\n" +
            "【添加示例】\n" +
            "  /givebuff all scanup              ← 省略秒数，默认 3600 秒\n" +
            "  /givebuff all scanup 3600         ← 3600 秒为上限，超出会被钳制\n" +
            "  /givebuff #5 slow 5";

        public void Execute(string[] args, WebConsole console)
        {
            if (args.Length == 0)
            {
                console.Log(BuffList, LogLevel.Info);
                return;
            }

            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行。", LogLevel.Warning);
                return;
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players.Count == 0)
            {
                console.Log("当前没有活动的游戏房间或玩家。", LogLevel.Warning);
                return;
            }

            bool targetAll = true;
            int targetId = -1;
            int argIdx = 0;

            // 目标
            if (argIdx < args.Length)
            {
                string t = args[argIdx];
                if (t.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    targetAll = true;
                    argIdx++;
                }
                else if (t.StartsWith("#") && int.TryParse(t.Substring(1), out int pid))
                {
                    targetAll = false;
                    targetId = pid;
                    argIdx++;
                }
                else
                {
                    console.Log($"无效的目标: {t}（应为 all 或 #<playerId>）", LogLevel.Warning);
                    return;
                }
            }
            else
            {
                console.Log($"用法: {Usage}", LogLevel.Warning);
                return;
            }

            if (argIdx >= args.Length)
            {
                console.Log($"用法: {Usage}", LogLevel.Warning);
                return;
            }

            string second = args[argIdx];

            // /givebuff <target> clear  → 清全部
            if (IsClearToken(second))
            {
                int cleared = ApplyToTargets(room, targetAll, targetId, console, player =>
                {
                    player.BuffComponent.Clear(isBroadcast: true);
                }, requireAlive: false);

                if (cleared < 0) return;
                if (targetAll)
                    console.Log($"已清除 {cleared} 名玩家的全部 BUFF。", LogLevel.Message);
                else
                    console.Log($"已清除玩家全部 BUFF。", LogLevel.Message);
                return;
            }

            // 指定 BUFF 类型
            if (!TryParseBuff(second, out EBuffType buffType))
            {
                console.Log($"未知 BUFF: {second}（直接输入 /givebuff 查看可用列表）", LogLevel.Warning);
                return;
            }
            argIdx++;

            // 第三参（可选）：clear / 0 → 清该 BUFF；正整数秒 → 添加；省略 → 使用最大时长
            int seconds;
            if (argIdx >= args.Length)
            {
                seconds = MaxDurationSeconds;
            }
            else
            {
                string third = args[argIdx];
                if (IsClearToken(third) || (int.TryParse(third, out int zeroCheck) && zeroCheck == 0))
                {
                    int cleared = ApplyToTargets(room, targetAll, targetId, console, player =>
                    {
                        player.BuffComponent.RemoveBuffForce(buffType);
                    }, requireAlive: false);

                    if (cleared < 0) return;
                    if (targetAll)
                        console.Log($"已从 {cleared} 名玩家清除 BUFF={buffType}。", LogLevel.Message);
                    else
                        console.Log($"已清除 BUFF={buffType}。", LogLevel.Message);
                    return;
                }

                if (!int.TryParse(third, out seconds) || seconds < 0)
                {
                    console.Log($"持续时间无效，应为非负整数（单位：秒；0 / clear 表示清除；省略则使用最大 {MaxDurationSeconds} 秒）。", LogLevel.Warning);
                    return;
                }

                if (seconds > MaxDurationSeconds)
                {
                    console.Log($"时长 {seconds} 秒超过最大值，已修正为 {MaxDurationSeconds} 秒。", LogLevel.Debug);
                    seconds = MaxDurationSeconds;
                }
            }

            // seconds > 0：添加（先强制移除再加，以刷新时长）
            int durationMs = seconds * 1000;
            int count = ApplyToTargets(room, targetAll, targetId, console, player =>
            {
                // 原版 AddBuff 在已有同类时直接忽略；这里先清再加，保证时长被刷新
                if (player.BuffComponent.HasBuff(buffType))
                    player.BuffComponent.RemoveBuffForce(buffType);

                player.BuffComponent.AddBuff(buffType, durationMs);

                // 大厅 AlivePlayers 为空时 Flush 不会扫到任何人，用 JobTimer 兜底到期
                int pid = player.PublicInfo.PlayerId;
                EBuffType typeCopy = buffType;
                GameRoom.Instance.PushAfter(durationMs, () =>
                {
                    var p = GameRoom.Instance.Players.Find(x => x?.PublicInfo?.PlayerId == pid);
                    if (p == null) return;
                    if (p.BuffComponent.HasBuff(typeCopy))
                        p.BuffComponent.RemoveBuffForce(typeCopy);
                });
            }, requireAlive: true);

            if (count < 0) return;
            if (targetAll)
                console.Log($"已给 {count} 名玩家添加 BUFF={buffType}，持续 {seconds} 秒。", LogLevel.Message);
            else
                console.Log($"已添加 BUFF={buffType}，持续 {seconds} 秒。", LogLevel.Message);
        }

        /// <summary>
        /// 对 all 或指定玩家执行 action。找不到目标时返回 -1；否则返回处理人数。
        /// </summary>
        private static int ApplyToTargets(
            GameRoom room,
            bool targetAll,
            int targetId,
            WebConsole console,
            Action<Server.Game.Player> action,
            bool requireAlive)
        {
            if (targetAll)
            {
                int count = 0;
                foreach (var player in room.Players)
                {
                    if (player?.PublicInfo == null) continue;
                    if (requireAlive && !player.IsAlive) continue;
                    action(player);
                    count++;
                }
                return count;
            }

            var found = room.Players.Find(p => p?.PublicInfo?.PlayerId == targetId);
            if (found == null)
            {
                console.Log($"找不到 PlayerId={targetId} 的玩家。", LogLevel.Warning);
                return -1;
            }

            if (requireAlive && !found.IsAlive)
            {
                console.Log($"玩家 {found.Name}（#{targetId}）已死亡，无法添加 BUFF。", LogLevel.Warning);
                return -1;
            }

            action(found);
            return 1;
        }

        private static bool IsClearToken(string s)
        {
            return s.Equals("clear", StringComparison.OrdinalIgnoreCase)
                || s.Equals("remove", StringComparison.OrdinalIgnoreCase)
                || s.Equals("rm", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseBuff(string s, out EBuffType buffType)
        {
            if (BuffAliases.TryGetValue(s, out buffType)) return true;
            return Enum.TryParse(s, ignoreCase: true, out buffType) && Enum.IsDefined(typeof(EBuffType), buffType);
        }
    }
}
