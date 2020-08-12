using System;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    public interface ICache
    {
        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        ValueTask<T> GetSetAsync<T>(string key, Func<ValueTask<T>> calculateValue, TimeSpan duration);

        /// <summary>
        /// Invalidates the specified key.
        /// </summary>
        ValueTask Invalidate(string key);

        /// <summary>
        /// An event that is called every time a cache key is invalidated.
        /// </summary>
        event Action<string> KeyInvalidated;
    }

    public static class CacheExtensions
    {
        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="durationInSeconds">Indicates how long, in seconds, the value should be kept in the cache. Use null to prevent expiration.</param>
        /// <returns></returns>
        public static ValueTask<T> GetSetAsync<T>(this ICache cache, string key, Func<ValueTask<T>> calculateValue, int? durationInSeconds = null)
        {
            var duration = durationInSeconds is object ? TimeSpan.FromSeconds(durationInSeconds.Value) : TimeSpan.MaxValue;
            return cache.GetSetAsync(key, calculateValue, duration);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        public static ValueTask<T> GetSet<T>(this ICache cache, string key, Func<T> calculateValue, TimeSpan duration)
        {
            return cache.GetSetAsync(key, () => new ValueTask<T>(calculateValue()), duration);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="durationInSeconds">Indicates how long, in seconds, the value should be kept in the cache. Use null to prevent expiration.</param>
        /// <returns></returns>
        public static ValueTask<T> GetSet<T>(this ICache cache, string key, Func<T> calculateValue, int? durationInSeconds = null)
        {
            var duration = durationInSeconds is object ? TimeSpan.FromSeconds(durationInSeconds.Value) : TimeSpan.MaxValue;
            return cache.GetSetAsync(key, () => new ValueTask<T>(calculateValue()), duration);
        }
    }
}
