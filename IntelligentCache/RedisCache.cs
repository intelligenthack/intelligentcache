using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values on Redis.
    /// </summary>
    public class RedisCache : ICache, IDisposable
    {
        private readonly IConnectionMultiplexer redis;
        private readonly MultiKeyLock syncLock = new MultiKeyLock();
        private readonly string prefix;
        private bool disposedValue;

        public IRedisSerializer Serializer { get; set; } = new JsonStringSerializer();

        /// <summary>
        /// Creates a cache that is stored on a Redis instance.
        /// </summary>
        /// <param name="prefix">This string is prefixed to the key names to partition the keys if the underlying storage is shared</param>
        public RedisCache(IConnectionMultiplexer redis, string prefix)
        {
            this.redis = redis ?? throw new ArgumentNullException(nameof(redis));
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
            this.prefix = prefix + ":";
        }

        public async Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var db = redis.GetDatabase();
            var k = prefix + key;

            syncLock.EnterReadLock(k);
            try
            {
                var res = await db.StringGetAsync(k).ConfigureAwait(false);
                if (res.HasValue) return Serializer.Deserialize<T>(res);
            }
            finally
            {
                syncLock.ExitReadLock(k);
            }

            syncLock.EnterUpgradeableReadLock(k);
            try
            {
                var res = await db.StringGetAsync(k).ConfigureAwait(false);
                if (res.HasValue) return Serializer.Deserialize<T>(res);

                syncLock.EnterWriteLock(k);
                try
                {
                    var value = await calculateValue(cancellationToken).ConfigureAwait(false);
                    if (value == null) return null; // Not all caches support null values. Also, caching a null is dodgy in itself.

                    await db.StringSetAsync(k, Serializer.Serialize(value), duration).ConfigureAwait(false);
                    return value;
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

        public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            var db = redis.GetDatabase();
            var k = prefix + key;
            syncLock.EnterWriteLock(k);
            try
            {
                await db.StringSetAsync(k, RedisValue.Null).ConfigureAwait(false);
            }
            finally
            {
                syncLock.ExitWriteLock(k);
            }
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            var db = redis.GetDatabase();
            var k = prefix + key;

            syncLock.EnterReadLock(k);
            try
            {
                var res = db.StringGet(k);
                if (res.HasValue) return Serializer.Deserialize<T>(res);
            }
            finally
            {
                syncLock.ExitReadLock(k);
            }

            syncLock.EnterUpgradeableReadLock(k);
            try
            {
                var res = db.StringGet(k);
                if (res.HasValue) return Serializer.Deserialize<T>(res);

                syncLock.EnterWriteLock(k);
                try
                {
                    var value = calculateValue();
                    if (value == null) return null; // Not all caches support null values. Also, caching a null is dodgy in itself.

                    db.StringSet(k, Serializer.Serialize(value), duration);
                    return value;
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

        public void Invalidate(string key)
        {
            var db = redis.GetDatabase();
            var k = prefix + key;
            syncLock.EnterWriteLock(k);
            try
            {
                db.StringSet(k, RedisValue.Null);
            }
            finally
            {
                syncLock.ExitWriteLock(k);
            }
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
