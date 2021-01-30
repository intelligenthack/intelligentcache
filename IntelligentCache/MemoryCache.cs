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
        private readonly object _synclock = new();

        /// <param name="prefix">A prefix that is inserted before each key to prevent collisions with other users of the shared cache.</param>
        public MemoryCache(string prefix)
        {
            if (prefix is null) throw new ArgumentNullException(nameof(prefix));

            _prefix = $"{prefix}:";
        }

        public T? GetSet<T>(string key, Func<T?> calculateValue, TimeSpan duration) where T : class
        {
            var k = $"{_prefix}{key}";
            var res = (T?)MemCache.Default.Get(k);
            
            if (res is not null) return res;
            
            lock (_synclock)
            {
                res = (T?)MemCache.Default.Get(k);

                if (res is not null) return res;
                
                res = calculateValue();
                
                if (res is null) return res;

                var expiration = duration == TimeSpan.MaxValue
                    ? DateTimeOffset.MaxValue
                    : DateTimeOffset.UtcNow.Add(duration);

                MemCache.Default.Set(k, res, expiration);
            }

            return res;
        }

        public void Invalidate(string key)
            => MemCache.Default.Remove($"{_prefix}{key}");

        public Task<T?> GetSetAsync<T>(string key, Func<CancellationToken, Task<T?>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
            => Task.FromResult(GetSet(key, () => calculateValue(cancellationToken).GetAwaiter().GetResult(), duration));

        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            Invalidate(key);
            return Task.CompletedTask;
        }
    }
}
