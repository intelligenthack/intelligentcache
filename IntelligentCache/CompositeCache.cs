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

            // Propagate invalidations on the second level to the first level.
            _level2.KeyInvalidated += key => _level1.Invalidate(key);
        }

        public ValueTask<T> GetSet<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
        {
            return _level1.GetSet(key,
                ct => _level2.GetSet(key, calculateValue, duration, ct),
                duration,
                cancellationToken
            );
        }

        public async ValueTask Invalidate(string key)
        {
            // The first level does not need to be invalidated because the KeyInvalidated event handler will do it.
            await _level2.Invalidate(key);
        }

        public event Action<string> KeyInvalidated
        {
            add
            {
                _level1.KeyInvalidated += value;
            }
            remove
            {
                _level1.KeyInvalidated -= value;
            }
        }
    }
}
