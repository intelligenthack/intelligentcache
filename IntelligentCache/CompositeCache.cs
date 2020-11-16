using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// Creates a two-level hierarchical cache.
    /// Values are retrieved first from the first level.
    /// If no value is found, the second level is used.
    /// </summary>
    public sealed class CompositeCache : ICache
    {
        private readonly ICache _level1;
        private readonly ICache _level2;

        /// <param name="level1">This is the first cache to be checked. If there is a cache miss, the second level cache will be attempted</param>
        /// <param name="level2">Second level cache (usually a shared/remote cache in a webfarm). Called when the first level cache misses.</param>
        public CompositeCache(ICache level1, ICache level2)
        {
            _level1 = level1 ?? throw new ArgumentNullException(nameof(level1));
            _level2 = level2 ?? throw new ArgumentNullException(nameof(level2));
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return _level1.GetSet(key, () => _level2.GetSet(key, calculateValue, duration), duration);
        }

        public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return _level1.GetSetAsync(key, ct => _level2.GetSetAsync(key, calculateValue, duration, ct), duration, cancellationToken);
        }

        public void Invalidate(string key)
        {
            _level2.Invalidate(key);
            _level1.Invalidate(key);
        }

        public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            await _level2.InvalidateAsync(key, cancellationToken).ConfigureAwait(false);
            await _level1.InvalidateAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }
}
