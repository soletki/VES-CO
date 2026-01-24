using System.Collections.Concurrent;

namespace VESCO.Timeline
{
    public class FrameCache<TKey, TValue> : IDisposable
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
        private readonly int _maxSize;
        private readonly object _lockObj = new();
        private long _totalMemoryBytes;

        private class CacheEntry
        {
            public TValue Value { get; set; }
            public long MemoryBytes { get; set; }
            public DateTime LastAccessTime { get; set; }
        }

        public FrameCache(int maxSizeBytes = 500_000_000) // 500 MB default
        {
            _maxSize = maxSizeBytes;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            value = default;

            if (_cache.TryGetValue(key, out var entry))
            {
                lock (_lockObj)
                {
                    entry.LastAccessTime = DateTime.UtcNow;
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }

        public void Add(TKey key, TValue value, long estimatedMemoryBytes)
        {
            if (value == null)
                return;

            lock (_lockObj)
            {
                // Remove if key already exists
                if (_cache.TryRemove(key, out var oldEntry))
                {
                    _totalMemoryBytes -= oldEntry.MemoryBytes;
                }

                // Evict LRU entries if needed
                while (_totalMemoryBytes + estimatedMemoryBytes > _maxSize && _cache.Count > 0)
                {
                    var lruKey = _cache
                        .OrderBy(x => x.Value.LastAccessTime)
                        .First()
                        .Key;

                    if (_cache.TryRemove(lruKey, out var evictedEntry))
                    {
                        _totalMemoryBytes -= evictedEntry.MemoryBytes;
                    }
                }

                var entry = new CacheEntry
                {
                    Value = value,
                    MemoryBytes = estimatedMemoryBytes,
                    LastAccessTime = DateTime.UtcNow
                };

                _cache.TryAdd(key, entry);
                _totalMemoryBytes += estimatedMemoryBytes;
            }
        }

        public void Clear()
        {
            lock (_lockObj)
            {
                _cache.Clear();
                _totalMemoryBytes = 0;
            }
        }

        public int Count => _cache.Count;

        public long TotalMemoryBytes => _totalMemoryBytes;

        public void Dispose()
        {
            Clear();
        }
    }
}