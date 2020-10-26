using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
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

        public MemoryCache()
        {
            this.Clock = WallClock.Instance;
        }

        public IClock Clock { get; set; }

        private sealed class CacheEntry
        {
            private JoinableTaskCompletionSource<object?>? _valuePromise;
            private DateTime _expiration;

            private bool GetSetLocked(DateTime now, TimeSpan duration, out JoinableTaskCompletionSource<object?> currentValuePromise)
            {
                lock (this)
                {
                    if (this._valuePromise is null || _expiration <= now)
                    {
                        currentValuePromise = new JoinableTaskCompletionSource<object?>();
                        _valuePromise = currentValuePromise;
                        _expiration = duration != TimeSpan.MaxValue
                            ? now.Add(duration)
                            : DateTime.MaxValue;
                        return true;
                    }
                    else
                    {
                        currentValuePromise = this._valuePromise;
                    }
                    return false;
                }
            }

            public async ValueTask<T> GetSetAsync<T>(DateTime now, Func<CancellationToken, ValueTask<T>> calculateValueAsync, TimeSpan duration, CancellationToken cancellationToken)
            {
                var currentValuePromise = this._valuePromise;
                if (currentValuePromise is null || _expiration <= now)
                {
                    bool shouldCalculateValue = GetSetLocked(now, duration, out currentValuePromise);
                    if (shouldCalculateValue)
                    {
                        try
                        {
                            var value = await calculateValueAsync(cancellationToken);
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

            public T GetSet<T>(DateTime now, Func<T> calculateValue, TimeSpan duration)
            {
                var currentValuePromise = this._valuePromise;
                if (currentValuePromise is null || _expiration <= now)
                {
                    bool shouldCalculateValue = GetSetLocked(now, duration, out currentValuePromise);
                    if (shouldCalculateValue)
                    {
                        try
                        {
                            var value = calculateValue();
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
                return (T)currentValuePromise.GetResult()!;
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

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
        {
            var entry = _entries.GetOrAdd(key, _ => new CacheEntry());
            return entry.GetSetAsync(this.Clock.UtcNow, calculateValue, duration, cancellationToken);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
        {
            var entry = _entries.GetOrAdd(key, _ => new CacheEntry());
            return entry.GetSet(this.Clock.UtcNow, calculateValue, duration);
        }

        public ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default)
        {
            Invalidate(key, wasTriggeredLocally);
            return default;
        }

        public void Invalidate(string key, bool wasTriggeredLocally = true)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.Invalidate();
            }
        }
    }

    internal class JoinableTaskCompletionSource<T>
    {
        private readonly object _resultAvailableMonitor = new object();
        private readonly TaskCompletionSource<T> _taskCompletionSource = new TaskCompletionSource<T>();

        public void SetResult(T value)
        {
            lock (_resultAvailableMonitor)
            {
                _taskCompletionSource.SetResult(value);
                Monitor.PulseAll(_resultAvailableMonitor);
            }
        }

        public void SetException(Exception ex)
        {
            lock (_resultAvailableMonitor)
            {
                _taskCompletionSource.SetException(ex);
                Monitor.PulseAll(_resultAvailableMonitor);
            }
        }

        public Task<T> Task => _taskCompletionSource.Task;

        public T GetResult()
        {
            var task = _taskCompletionSource.Task;
            if (!task.IsCompleted)
            {
                lock (_resultAvailableMonitor)
                {
                    if (!task.IsCompleted)
                    {
                        Monitor.Wait(_resultAvailableMonitor);
                    }
                }
            }

            if (task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
            else
            {
                return task.Result;
            }
        }
    }
}
