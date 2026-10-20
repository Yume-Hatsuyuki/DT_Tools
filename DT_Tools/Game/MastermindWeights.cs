using System;
using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    /// <summary>
    /// 黑幕抽取权重（房主侧，仅当前房间有效）。权重即玩家拥有的格子数，抽取即“从格子中随机抽一格”。
    /// </summary>
    /// <remarks>
    /// 键为玩家 AccountID（Steam 账号；重新进房后保持不变，0.1.18c Server.Game/Player.cs:566）。
    /// 权重使用整数，避免百分比的精度问题；新玩家入场默认拥有 defaultWeight 个格子，互不影响。
    /// 抽取点：GameRoom.SetMasterMind（0.1.18c Server.Game/GameRoom.cs:326）。原版 StartPick 在
    /// GameRoom.cs:1610-1612 随机选出黑幕后调用它；SpectatorJoin 的 StartPick 替换版同样经由它
    /// （Features/SpectatorJoin/Feature.GameStart.cs:192）。迁移恢复也经由它（MigrationOrchestrator.cs:196），需用 IsMigrating 区分。
    /// </remarks>
    public static class MastermindWeights
    {
        /// <summary>权重上限：防止误输入过大导致其他玩家几乎不可能成为黑幕。</summary>
        public const int MaxWeight = 1000;

        private static readonly Dictionary<string, int> _weights = new Dictionary<string, int>();

        /// <summary>取得玩家的格子数（未单独设置则为 defaultWeight）。</summary>
        public static int Get(string accountId, int defaultWeight)
        {
            if (!string.IsNullOrEmpty(accountId) && _weights.TryGetValue(accountId, out int weight))
                return weight;
            return Clamp(defaultWeight);
        }

        /// <summary>设置格子数。输入被钳制到 [0, MaxWeight]。</summary>
        public static void Set(string accountId, int weight) => _weights[accountId] = Clamp(weight);

        /// <summary>清空所有单独设置（恢复为默认格子数）。</summary>
        public static void Clear() => _weights.Clear();

        /// <summary>
        /// 从候选中抽取黑幕：每人占 weight 个格子，随机抽一格，格子所属者即为黑幕。候选为空时返回 null。
        /// </summary>
        public static Server.Game.Player Pick(IReadOnlyList<Server.Game.Player> pool, int defaultWeight, Func<float> roll)
        {
            if (pool == null || pool.Count == 0)
                return null;
            var weights = pool.Select(p => Get(p?.AccountID, defaultWeight)).ToList();
            return pool[PickIndex(weights, roll)];
        }

        /// <summary>纯函数：格子总数为 0（所有人权重均为 0）时，抽取会回退为均匀分布。</summary>
        public static bool AllZero(IReadOnlyList<int> weights) => weights.All(w => w <= 0);

        /// <summary>
        /// 纯函数：从格子中抽一格，返回格子所属者的下标。roll 返回 [0,1)。
        /// 权重 0 的候选没有格子，永不被选中；全部为 0 时回退为均匀抽取，保证对局可以继续。
        /// </summary>
        public static int PickIndex(IReadOnlyList<int> weights, Func<float> roll)
        {
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("候选为空", nameof(weights));

            float r = ClampRoll(roll());
            int total = weights.Sum(w => Math.Max(0, w));
            if (total <= 0)
                return Math.Min(weights.Count - 1, (int)(r * weights.Count));

            int ticket = Math.Min(total - 1, (int)(r * total));
            int acc = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                int w = Math.Max(0, weights[i]);
                acc += w;
                if (w > 0 && ticket < acc)
                    return i;
            }
            return weights.Count - 1;
        }

        /// <summary>纯函数：第 index 位玩家的理论概率（格子数 ÷ 总格子数）；全部为 0 时为均匀分布。</summary>
        public static float Share(IReadOnlyList<int> weights, int index)
        {
            int total = weights.Sum(w => Math.Max(0, w));
            if (total <= 0)
                return 1f / weights.Count;
            return Math.Max(0, weights[index]) / (float)total;
        }

        private static int Clamp(int weight) => Math.Min(MaxWeight, Math.Max(0, weight));

        private static float ClampRoll(float r)
        {
            if (float.IsNaN(r) || r < 0f)
                return 0f;
            return r >= 1f ? 0.999999f : r;
        }
    }
}
