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

        public CompositeCache(ICache level1, ICache level2)
        {
            _level1 = level1;
            _level2 = level2;
        }
        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
        {
            return _level1.GetSetAsync(key,
                ct => _level2.GetSetAsync(key, calculateValue, duration, ct),
                duration,
                cancellationToken
            );
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
        {
            return _level1.GetSet(key,
                () =>_level2.GetSet(key, calculateValue, duration),
                duration
            );
        }

        public async ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default)
        {
            await _level2.InvalidateAsync(key, wasTriggeredLocally, cancellationToken);
            await _level1.InvalidateAsync(key, wasTriggeredLocally, cancellationToken);
        }

        public void Invalidate(string key, bool wasTriggeredLocally = true)
        {
            _level2.Invalidate(key, wasTriggeredLocally);
            _level1.Invalidate(key, wasTriggeredLocally);
        }
    }
}
