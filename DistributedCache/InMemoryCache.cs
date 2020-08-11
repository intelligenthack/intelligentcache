using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// Adapts an <see cref="IMemoryCache" /> to <see cref="ICache" />.
    /// </summary>
    public sealed class InMemoryCache : ICache
    {
        private readonly IMemoryCache _memoryCache;

        public InMemoryCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public event Action<string>? KeyInvalidated;

        public ValueTask<T> GetSet<T>(string key, Func<ValueTask<T>> setAction, TimeSpan duration)
        {
            if (_memoryCache.TryGetValue(key, out var value))
            {
                return new ValueTask<T>((T)value);
            }

            return Set(key, setAction, duration);
        }

        private async ValueTask<T> Set<T>(string key, Func<ValueTask<T>> setAction, TimeSpan duration)
        {
            var value = await setAction();
            if (duration != TimeSpan.MaxValue)
            {
                _memoryCache.Set(key, value, duration);
            }
            else
            {
                _memoryCache.Set(key, value);
            }
            return value;
        }

        public ValueTask Invalidate(string key)
        {
            _memoryCache.Remove(key);
            KeyInvalidated?.Invoke(key);
            return default;
        }
    }
}
