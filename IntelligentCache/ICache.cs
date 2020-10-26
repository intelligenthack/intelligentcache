using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
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
        ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default);
        T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration);

        /// <summary>
        /// Invalidates the specified key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="wasTriggeredLocally">Differentiate if the invalidation was originated in this instance.</param>
        ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates the specified key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="wasTriggeredLocally">Differentiate if the invalidation was originated in this instance.</param>
        void Invalidate(string key, bool wasTriggeredLocally = true);
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
        public static ValueTask<T> GetSetAsync<T>(this ICache cache, string key, Func<CancellationToken, ValueTask<T>> calculateValue, int? durationInSeconds = null, CancellationToken cancellationToken = default)
        {
            var duration = durationInSeconds is object ? TimeSpan.FromSeconds(durationInSeconds.Value) : TimeSpan.MaxValue;
            return cache.GetSetAsync(key, calculateValue, duration, cancellationToken);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="durationInSeconds">Indicates how long, in seconds, the value should be kept in the cache. Use null to prevent expiration.</param>
        /// <returns></returns>
        public static T GetSet<T>(this ICache cache, string key, Func<T> calculateValue, int? durationInSeconds = null)
        {
            var duration = durationInSeconds is object ? TimeSpan.FromSeconds(durationInSeconds.Value) : TimeSpan.MaxValue;
            return cache.GetSet(key, calculateValue, duration);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        public static ValueTask<T> GetSetAsync<T>(this ICache cache, string key, Func<T> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return cache.GetSetAsync(key, _ => new ValueTask<T>(calculateValue()), duration, cancellationToken);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        public static T GetSet<T>(this ICache cache, string key, Func<T> calculateValue, TimeSpan duration)
        {
            return cache.GetSet(key, calculateValue, duration);
        }

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="durationInSeconds">Indicates how long, in seconds, the value should be kept in the cache. Use null to prevent expiration.</param>
        /// <returns></returns>
        public static ValueTask<T> GetSetAsync<T>(this ICache cache, string key, Func<T> calculateValue, int? durationInSeconds = null, CancellationToken cancellationToken = default)
        {
            var duration = durationInSeconds is object ? TimeSpan.FromSeconds(durationInSeconds.Value) : TimeSpan.MaxValue;
            return cache.GetSetAsync(key, _ => new ValueTask<T>(calculateValue()), duration, cancellationToken);
        }
    }
}
