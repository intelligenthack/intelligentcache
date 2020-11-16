using IntelligentHack.IntelligentCache;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentCache.Tests
{
    public class InspectableCache : ICache
    {
        private readonly Action<string> _onCall;
        private readonly bool _cacheMiss;

        public InspectableCache(Action<string> onCall, bool cacheMiss = false)
        {
            _onCall = onCall ?? throw new ArgumentNullException(nameof(onCall));
            _cacheMiss = cacheMiss;
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T: class
        {
            _onCall(key);
            if (_cacheMiss) calculateValue();
            return default!;
        }

        public async Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T: class
        {
            _onCall(key);
            if (_cacheMiss) await calculateValue(CancellationToken.None);
            return default!;
        }

        public void Invalidate(string key)
        {
            _onCall(key);
        }

        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            _onCall(key);
            return Task.CompletedTask;
        }
    }
}
