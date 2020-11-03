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
        public TimeSpan CacheDuration { get; set; }

        public CompositeCache(ICache level1, ICache level2)
        {
            _level1 = level1 ?? throw new ArgumentNullException(nameof(level1));
            _level2 = level2 ?? throw new ArgumentNullException(nameof(level2));
            this.CacheDuration = TimeSpan.FromHours(1);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T: class
        {
            return _level1.GetSet(key, () => _level2.GetSet(key, calculateValue, duration), duration);
        }

        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T: class
        {
            return await _level1.GetSetAsync(key, async (c) => await _level2.GetSetAsync(key, calculateValue, duration, c), duration, cancellationToken);
        }

        public void Invalidate(string key)
        {
            _level2.Invalidate(key);
            _level1.Invalidate(key);
        }

        public async ValueTask InvalidateAsync(string key)
        {
            await _level2.InvalidateAsync(key);
            await _level1.InvalidateAsync(key);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, CancellationToken cancellationToken = default) where T : class
        {
            return this.GetSetAsync(key, calculateValue, this.CacheDuration, cancellationToken);
        }

        public T GetSet<T>(string key, Func<T> calculateValue) where T : class
        {
            return this.GetSet(key,calculateValue);
        }
    }
}
