using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    public sealed class MemoryCache : ICache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new ConcurrentDictionary<string, CacheEntry>();
        private readonly IClock _clock;

        public MemoryCache() : this(WallClock.Instance)
        {
        }

        public MemoryCache(IClock clock)
        {
            _clock = clock;
        }

        private sealed class CacheEntry
        {
            private TaskCompletionSource<object?>? _valuePromise;
            private DateTime _expiration;

            public async ValueTask<T> GetSet<T>(DateTime now, Func<ValueTask<T>> calculateValue, TimeSpan duration)
            {
                var currentValuePromise = this._valuePromise;
                if (currentValuePromise is null || _expiration <= now)
                {
                    bool shouldCalculateValue;
                    lock (this)
                    {
                        currentValuePromise = this._valuePromise;
                        if (currentValuePromise is null || _expiration <= now)
                        {
                            shouldCalculateValue = true;
                            currentValuePromise = new TaskCompletionSource<object?>();
                            _valuePromise = currentValuePromise;
                            _expiration = duration != TimeSpan.MaxValue
                                ? now.Add(duration)
                                : DateTime.MaxValue;
                        }
                        else
                        {
                            shouldCalculateValue = false;
                        }
                    }

                    if (shouldCalculateValue)
                    {
                        try
                        {
                            var value = await calculateValue();
                            currentValuePromise.SetResult(value);
                            return value;
                        }
                        catch (Exception ex)
                        {
                            // Propagate the exception to the other threads that are waiting for this result.
                            currentValuePromise.SetException(ex);
                            lock (this)
                            {
                                _valuePromise = null;
                            }
                            throw;
                        }
                    }
                }

                // Someone else is calculating the value (or has already done so), just wait for it.
                // If we reach this point, currentValuePromise is not null.
                return (T)(await currentValuePromise.Task)!;
            }

            public bool Invalidate()
            {
                lock (this)
                {
                    if (_valuePromise is object)
                    {
                        _valuePromise = null;
                        _expiration = DateTime.MinValue;
                        return true;
                    }
                }

                return false;
            }
        }

        public event Action<string>? KeyInvalidated;

        public ValueTask<T> GetSetAsync<T>(string key, Func<ValueTask<T>> calculateValue, TimeSpan duration)
        {
            var entry = _entries.GetOrAdd(key, _ => new CacheEntry());
            return entry.GetSet(_clock.UtcNow, calculateValue, duration);
        }

        public ValueTask Invalidate(string key)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Invalidate())
                {
                    KeyInvalidated?.Invoke(key);
                }
            }
            return default;
        }
    }
}
