using System;

namespace DT_Tools.Core
{
    /// <summary>
    /// 线程安全的定长环形缓冲（全项目唯一实现）。
    /// 写满后覆盖最旧条目；Seq 每次写入/清空自增，供调用方做增量拉取。
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _buf;
        private readonly object _gate = new object();
        private int _start;
        private int _count;

        public long Seq { get; private set; }

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buf = new T[capacity];
        }

        public void Append(T item)
        {
            lock (_gate)
            {
                int idx;
                if (_count == _buf.Length)
                {
                    _start = (_start + 1) % _buf.Length;
                    idx = (_start + _count - 1) % _buf.Length;
                }
                else
                {
                    idx = (_start + _count) % _buf.Length;
                    _count++;
                }
                _buf[idx] = item;
                Seq++;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _start = 0;
                _count = 0;
                Array.Clear(_buf, 0, _buf.Length);
                Seq++;
            }
        }

        public (long seq, T[] items) Snapshot()
        {
            lock (_gate)
            {
                var items = new T[_count];
                for (int i = 0; i < _count; i++)
                    items[i] = _buf[(_start + i) % _buf.Length];
                return (Seq, items);
            }
        }
    }
}
