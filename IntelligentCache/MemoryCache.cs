using System;
using System.Threading;
using System.Threading.Tasks;
using MemCache = System.Runtime.Caching.MemoryCache;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values in memory.
    /// </summary>
    public class MemoryCache : ICache
    {
        private readonly string _prefix;
        private readonly object _synclock = new object();

        public MemoryCache(string prefix)
        {
            _prefix = prefix + ":";
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            var k = _prefix + key;
            var res = (T)MemCache.Default.Get(k);
            if (res == null)
            { 
                lock (_synclock)
                {
                    res = (T)MemCache.Default.Get(k);
                    if (res == null)
                    {
                        res = calculateValue();
                        MemCache.Default.Set(k, res, DateTimeOffset.UtcNow.Add(duration));
                    }
                }
            }

            return res;
        }
        public void Invalidate(string key)
        {
            var k = _prefix + key;
            MemCache.Default.Remove(k);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var result = GetSet(key, () => calculateValue(cancellationToken).GetAwaiter().GetResult(), duration);
            return new ValueTask<T>(result);
        }


        public ValueTask InvalidateAsync(string key)
        {
            Invalidate(key);
            return default;
        }
    }
}
