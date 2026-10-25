using System;
using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    public static class MastermindWeights
    {
        public const int MaxWeight = 1000;

        private static readonly Dictionary<string, int> _weights = new Dictionary<string, int>();

        public static int Get(string accountId, int defaultWeight)
        {
            if (!string.IsNullOrEmpty(accountId) && _weights.TryGetValue(accountId, out int weight))
                return weight;
            return Clamp(defaultWeight);
        }

        public static void Set(string accountId, int weight) => _weights[accountId] = Clamp(weight);

        public static void Clear() => _weights.Clear();

        public static Server.Game.Player Pick(IReadOnlyList<Server.Game.Player> pool, int defaultWeight, Func<float> roll)
        {
            if (pool == null || pool.Count == 0)
                return null;
            var weights = pool.Select(p => Get(p?.AccountID, defaultWeight)).ToList();
            return pool[PickIndex(weights, roll)];
        }

        public static bool AllZero(IReadOnlyList<int> weights) => weights.All(w => w <= 0);

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
