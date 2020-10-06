using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values in memory.
    /// </summary>
    /// <remarks>
    /// While this implementation supports expiration, expired items are never removed from the cache.
    /// This means that if many different cache keys are used, the memory usage will keep growing.
    /// </remarks>
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

            public async ValueTask<T> GetSet<T>(DateTime now, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
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
                            var value = await calculateValue(cancellationToken);
                            currentValuePromise.SetResult(value);
                            return value;
                        }
                        catch (Exception ex)
                        {
                            // Propagate the exception to the other threads that are waiting for this result.
                            lock (this)
                            {
                                _valuePromise = null;
                            }
                            currentValuePromise.SetException(ex);
                            throw;
                        }
                    }
                }

                // Someone else is calculating the value (or has already done so), just wait for it.
                // If we reach this point, currentValuePromise is not null.
                if (cancellationToken.CanBeCanceled)
                {
                    // Honour the provided cancellation token
                    var cancellation = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    cancellationToken.Register(() => cancellation.SetCanceled());
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.WhenAny(currentValuePromise.Task, cancellation.Task);
                    cancellationToken.ThrowIfCancellationRequested();
                    return (T)currentValuePromise.Task.Result!;
                }
                else
                {
                    return (T)(await currentValuePromise.Task)!;
                }
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

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
        {
            var entry = _entries.GetOrAdd(key, _ => new CacheEntry());
            return entry.GetSet(_clock.UtcNow, calculateValue, duration, cancellationToken);
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
