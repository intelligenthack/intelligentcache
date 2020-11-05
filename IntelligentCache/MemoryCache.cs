using System;
using System.Runtime.Caching;
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

                        var expiration = duration == TimeSpan.MaxValue
                            ? DateTimeOffset.MaxValue
                            : DateTimeOffset.UtcNow.Add(duration);

                        MemCache.Default.Set(k, res, expiration);
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

        public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var result = GetSet(key, () => calculateValue(cancellationToken).GetAwaiter().GetResult(), duration);
            return Task.FromResult(result);
        }

        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            Invalidate(key);
            return Task.CompletedTask;
        }
    }
}
