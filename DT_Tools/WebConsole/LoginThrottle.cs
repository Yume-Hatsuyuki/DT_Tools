using System;
using System.Collections.Generic;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 密码失败次数限制（防爆破）：按来源 IP 计数，窗口内失败过多则临时锁定；
    /// 另有全局计数，防止从多个 IP 分布式尝试。
    /// </summary>
    /// <remarks>
    /// 成功登录会清零该 IP 的计数。条目数量有上限，过期条目会被清理，避免被用来耗尽内存。
    /// 全局锁定期间所有来源都会被拒绝，这是对多 IP 尝试的代价：合法用户也会短暂受限。
    /// </remarks>
    public sealed class LoginThrottle
    {
        /// <summary>
        /// 单个来源在窗口内允许的最大失败次数。
        /// </summary>
        public const int PerIpMaxFailures = 5;

        /// <summary>
        /// 全局（所有来源合计）在窗口内允许的最大失败次数。
        /// </summary>
        public const int GlobalMaxFailures = 50;

        /// <summary>
        /// 统计窗口长度。
        /// </summary>
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 单个来源被锁定的时长。
        /// </summary>
        public static readonly TimeSpan PerIpLock = TimeSpan.FromMinutes(15);

        /// <summary>
        /// 全局锁定的时长。
        /// </summary>
        public static readonly TimeSpan GlobalLock = TimeSpan.FromMinutes(5);

        private const int MaxTracked = 1024;
        private const string GlobalKey = "*";

        private sealed class State
        {
            public int Failures;
            public DateTime WindowStart;
            public DateTime LockedUntil;
        }

        private readonly Dictionary<string, State> _states =
            new Dictionary<string, State>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        /// <summary>
        /// 判断来源当前是否被锁定（单个来源或全局）。
        /// </summary>
        /// <param name="ip">来源 IP 文本。</param>
        /// <param name="now">当前时间（UTC）。</param>
        /// <param name="retryAfter">剩余锁定时长；未锁定时为零。</param>
        /// <returns>被锁定时为 true。</returns>
        public bool IsLocked(string ip, DateTime now, out TimeSpan retryAfter)
        {
            lock (_lock)
            {
                TimeSpan best = TimeSpan.Zero;
                foreach (var key in new[] { ip ?? "unknown", GlobalKey })
                {
                    if (_states.TryGetValue(key, out var st) && st.LockedUntil > now)
                    {
                        var left = st.LockedUntil - now;
                        if (left > best)
                        {
                            best = left;
                        }
                    }
                }
                retryAfter = best;
                return best > TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 记录一次失败，并在达到阈值时锁定。
        /// </summary>
        /// <param name="ip">来源 IP 文本。</param>
        /// <param name="now">当前时间（UTC）。</param>
        public void RecordFailure(string ip, DateTime now)
        {
            lock (_lock)
            {
                Bump(ip ?? "unknown", now, PerIpMaxFailures, PerIpLock);
                Bump(GlobalKey, now, GlobalMaxFailures, GlobalLock);
            }
        }

        /// <summary>
        /// 记录一次成功，清零该来源的失败计数。
        /// </summary>
        /// <param name="ip">来源 IP 文本。</param>
        public void RecordSuccess(string ip)
        {
            lock (_lock)
            {
                _states.Remove(ip ?? "unknown");
            }
        }

        private void Bump(string key, DateTime now, int max, TimeSpan lockFor)
        {
            if (!_states.TryGetValue(key, out var st))
            {
                if (_states.Count >= MaxTracked)
                {
                    Prune(now);
                }
                st = new State { WindowStart = now };
                _states[key] = st;
            }
            if (now - st.WindowStart > Window)
            {
                st.Failures = 0;
                st.WindowStart = now;
            }
            st.Failures++;
            if (st.Failures >= max)
            {
                st.LockedUntil = now + lockFor;
                st.Failures = 0;
                st.WindowStart = now;
            }
        }

        private void Prune(DateTime now)
        {
            var dead = new List<string>();
            foreach (var kv in _states)
            {
                if (kv.Value.LockedUntil <= now && now - kv.Value.WindowStart > Window)
                {
                    dead.Add(kv.Key);
                }
            }
            foreach (var k in dead)
            {
                _states.Remove(k);
            }
            if (_states.Count >= MaxTracked)
            {
                _states.Clear();
            }
        }
    }
}
