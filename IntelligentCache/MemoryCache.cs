using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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
        public System.Runtime.Caching.MemoryCache cache;

        public MemoryCache(string prefix)
        {
            cache = new System.Runtime.Caching.MemoryCache(prefix);
        }

        private readonly object obj = new object();

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
        {
            var res = (T) cache.Get(key);
            if (res == null)
                lock (obj)
                {
                    res = (T) cache.Get(key);
                    if (res == null)
                        res = calculateValue();
                    cache.Set(key, res, DateTimeOffset.UtcNow.Add(duration));
                }
            return res;
        }
        public void Invalidate(string key)
        {
            lock (obj)
            {
                cache.Remove(key);
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return GetSet(key, ()=>calculateValue(CancellationToken.None).GetAwaiter().GetResult(), duration);
        }


        public async ValueTask InvalidateAsync(string key)
        {
            Invalidate(key);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
