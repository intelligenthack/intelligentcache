using System;
using System.Threading;
using System.Threading.Tasks;
using MemCache = System.Runtime.Caching.MemoryCache;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values in a <see cref="System.Runtime.Caching.MemoryCache"/>.
    /// </summary>
    public class MemoryCache : ICache, IDisposable
    {
        private readonly string prefix;
        private readonly MultiKeyLock syncLock = new MultiKeyLock();
        private readonly MemCache innerCache = MemCache.Default;
        private bool disposedValue;

        /// <summary>
        /// Creates a cache that runs in the server memory.
        /// </summary>
        /// <param name="prefix">This string is prefixed to the key names to partition the keys if the underlying storage is shared</param>
        /// <param name="innerMemoryCache">If not null, the cache will use the given <see cref="System.Runtime.Caching.MemoryCache"/> instead of the default one.</param>
        public MemoryCache(string prefix, MemCache innerMemoryCache = null)
        {
            if (innerMemoryCache != null) innerCache = innerMemoryCache;
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
            this.prefix = prefix + ":";
        }

        /// <inheritdoc />
        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            var k = prefix + key;
            syncLock.EnterReadLock(k);
            try
            {
                var res = (T)innerCache.Get(k);
                if (res != null) return res;
            }
            finally
            {
                syncLock.ExitReadLock(k);
            }

            syncLock.EnterUpgradeableReadLock(k);
            try
            {
                var res = (T)innerCache.Get(k);
                if (res != null) return res;

                syncLock.EnterWriteLock(k);
                try
                {
                    res = calculateValue();
                    if (res == null) return null; // Not all caches support null values. Also, caching a null is dodgy in itself.

                    var expiration = duration == TimeSpan.MaxValue ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.Add(duration);
                    innerCache.Set(k, res, expiration);
                    return res;
                }
                finally
                {
                    syncLock.ExitWriteLock(k);
                }
            }
            finally
            {
                syncLock.ExitUpgradeableReadLock(k);
            }
        }

        /// <inheritdoc />
        public void Invalidate(string key)
        {
            var k = prefix + key;
            syncLock.EnterWriteLock(k);
            try
            {
                innerCache.Remove(k);
            }
            finally
            {
                syncLock.ExitWriteLock(k);
            }
        }

        /// <inheritdoc />
        public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var result = GetSet(key, () => calculateValue(cancellationToken).GetAwaiter().GetResult(), duration);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            Invalidate(key);
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    syncLock.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
