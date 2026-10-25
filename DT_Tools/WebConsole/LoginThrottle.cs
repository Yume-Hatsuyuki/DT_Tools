using System;
using System.Collections.Generic;

namespace DT_Tools.WebConsole
{
    public sealed class LoginThrottle
    {
        public const int PerIpMaxFailures = 5;

        public const int GlobalMaxFailures = 50;

        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        public static readonly TimeSpan PerIpLock = TimeSpan.FromMinutes(15);

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

        public void RecordFailure(string ip, DateTime now)
        {
            lock (_lock)
            {
                Bump(ip ?? "unknown", now, PerIpMaxFailures, PerIpLock);
                Bump(GlobalKey, now, GlobalMaxFailures, GlobalLock);
            }
        }

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
