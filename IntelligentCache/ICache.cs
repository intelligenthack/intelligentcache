using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    public interface ICache
    {
        /// <summary>
        /// Gets the value associated to the specified key asynchronously.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T: class;

        /// <summary>
        /// Gets the value associated to the specified key asynchronously.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <returns></returns>
        ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <param name="duration">Indicates how long the value should be kept in the cache. Use <see cref="TimeSpan.MaxValue"/> to prevent expiration.</param>
        /// <returns></returns>
        T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T: class;

        /// <summary>
        /// Gets the value associated to the specified key.
        /// If no value is currently associated, uses <paramref name="calculateValue"/> to retrieve it.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="calculateValue">A callback that produces a new value if the key is not in cache.</param>
        /// <returns></returns>
        T GetSet<T>(string key, Func<T> calculateValue) where T : class;


        /// <summary>
        /// Invalidates the specified key asynchronously.
        /// </summary>
        ValueTask InvalidateAsync(string key);

        /// <summary>
        /// Invalidates the specified key.
        /// </summary>
        void Invalidate(string key);

    }
}
