using System;
using System.Collections.Generic;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.GiveBuff
{
    /// <summary>
    /// /givebuff 参数：&lt;all|#id&gt; &lt;buff|clear&gt; [seconds|clear|0]
    /// 别名表对照 Protocol.EBuffType 全量收录（0.1.15b Protocol/EBuffType.cs，
    /// enum 声明在第 5 行、收尾括号在第 39 行，Slow..Footprint 全 16 项）。
    /// </summary>
    internal sealed class GiveBuffArgs
    {
        /// <summary>单次添加 BUFF 的最大时长（秒）。与原版 Raasrush/DetailCheck/MindControl
        /// 等被动技能挂载时长一致（3600000 ms）。</summary>
        public const int MaxDurationSeconds = 3600;

        public bool All { get; private set; }
        public int PlayerId { get; private set; }
        public GiveBuffAction Action { get; private set; }
        public EBuffType BuffType { get; private set; }
        public int Seconds { get; private set; }
        /// <summary>显式秒数超过上限被钳制时为 true（用于提示）。</summary>
        public bool Clamped { get; private set; }

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

        /// <summary>清除令牌（第二参=清全部；第三参=清该 BUFF）。</summary>
        public static bool IsClearToken(string s)
            => s.Equals("clear", StringComparison.OrdinalIgnoreCase)
               || s.Equals("remove", StringComparison.OrdinalIgnoreCase)
               || s.Equals("rm", StringComparison.OrdinalIgnoreCase);

        public static bool TryParseBuff(string s, out EBuffType buffType)
        {
            if (BuffAliases.TryGetValue(s, out buffType)) return true;
            return Enum.TryParse(s, ignoreCase: true, out buffType)
                   && Enum.IsDefined(typeof(EBuffType), buffType);
        }

        /// <summary>args 非空（目标 + buff 两参必备，第三参可选）。</summary>
        public static bool TryParse(string[] args, out GiveBuffArgs parsed, out string error)
        {
            parsed = new GiveBuffArgs();
            string target = args[0];
            if (TargetSpec.IsAll(target))
            {
                parsed.All = true;
            }
            else if (target.StartsWith("#") && int.TryParse(target.Substring(1), out int pid))
            {
                // 与旧实现一致：目标仅接受 all / #id（纯数字不算目标）
                parsed.All = false;
                parsed.PlayerId = pid;
            }
            else
            {
                error = $"无效的目标: {target}（应为 all 或 #<playerId>）";
                return false;
            }

            string second = args[1];
            if (IsClearToken(second))
            {
                parsed.Action = GiveBuffAction.ClearAll;
                error = null;
                return true;
            }
            if (!TryParseBuff(second, out EBuffType buffType))
            {
                error = $"未知 BUFF: {second}（直接输入 /givebuff 查看可用列表）";
                return false;
            }
            parsed.BuffType = buffType;

            // 第三参（可选）：clear / 0 → 清该 BUFF；正整数秒 → 添加；省略 → 使用最大时长
            if (args.Length < 3)
            {
                parsed.Action = GiveBuffAction.Add;
                parsed.Seconds = MaxDurationSeconds;
                error = null;
                return true;
            }

            string third = args[2];
            if (IsClearToken(third))
            {
                parsed.Action = GiveBuffAction.ClearOne;
                error = null;
                return true;
            }
            if (!int.TryParse(third, out int seconds) || seconds < 0)
            {
                error = $"持续时间无效，应为非负整数（单位：秒；0 / clear 表示清除；省略则使用最大 {MaxDurationSeconds} 秒）。";
                return false;
            }
            if (seconds == 0)
            {
                parsed.Action = GiveBuffAction.ClearOne;
                error = null;
                return true;
            }

            parsed.Action = GiveBuffAction.Add;
            if (seconds > MaxDurationSeconds)
            {
                parsed.Clamped = true;
                seconds = MaxDurationSeconds;
            }
            parsed.Seconds = seconds;
            error = null;
            return true;
        }
    }
}
