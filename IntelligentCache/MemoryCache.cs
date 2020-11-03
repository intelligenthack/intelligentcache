using System;
using System.Threading;
using System.Threading.Tasks;
using MemCache = System.Runtime.Caching.MemoryCache;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values in memory.
    /// </summary>
    /// <remarks>
    /// While this implementation supports expiration, expired items are never removed from the cache.
    /// This means that if many different cache keys are used, the memory usage will keep growing.
    /// </remarks>
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return GetSet(key, () => calculateValue(CancellationToken.None).GetAwaiter().GetResult(), duration);
        }


        public async ValueTask InvalidateAsync(string key)
        {
            Invalidate(key);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
