using System;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// Creates a two-level hierarchical cache.
    /// Values are retrieved first from the primary level.
    /// If no value is found, the secondary level is used.
    /// </summary>
    public sealed class CompositeCache : ICache
    {
        private readonly ICache _primary;
        private readonly ICache _secondary;

        public CompositeCache(ICache primary, ICache secondary)
        {
            _primary = primary;
            _secondary = secondary;

            // Propagate invalidations on the secondary to the primary.
            _secondary.KeyInvalidated += key => _primary.Invalidate(key);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<ValueTask<T>> calculateValue, TimeSpan duration)
        {
            return _primary.GetSetAsync(key,
                () => _secondary.GetSetAsync(key, calculateValue, duration),
                duration
            );
        }

        public async ValueTask Invalidate(string key)
        {
            // The primary does not need to be invalidated because the KeyInvalidated event handler will do it.
            await _secondary.Invalidate(key);
        }

        public event Action<string> KeyInvalidated
        {
            add
            {
                _primary.KeyInvalidated += value;
            }
            remove
            {
                _primary.KeyInvalidated -= value;
            }
        }
    }
}
