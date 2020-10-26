using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> based on Redis.
    /// </summary>
    public sealed class RedisCache : ICache
    {
        private readonly RedisConnection _redis;

        public string KeyPrefix { get; set; }
        public IRedisValueSerializer ValueSerializer { get; set; }

        public const string DefaultKeyPrefix = ":cache";

        public RedisCache(RedisConnection redis)
        {
            _redis = redis;

            KeyPrefix = DefaultKeyPrefix;
            ValueSerializer = DefaultRedisValueSerializer.Instance;
        }

        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValueAsync, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            if (_redis.TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = KeyPrefix + key;
                    var hit = await database.StringGetAsync(prefixedKey);
                    if (hit.HasValue && ValueSerializer.TryDeserialize<T>(hit, out var cachedValue))
                    {
                        return cachedValue;
                    }
                    else
                    {
                        var freshValue = await calculateValueAsync(cancellationToken);
                        var serializedValue = ValueSerializer.Serialize(freshValue);
                        var expiry = duration != TimeSpan.MaxValue ? duration : default(TimeSpan?);
                        await database.StringSetAsync(prefixedKey, serializedValue, expiry);
                        return freshValue;
                    }
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }

            // Fallback
            return await calculateValueAsync(cancellationToken);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
        {
            if (_redis.TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = KeyPrefix + key;
                    var hit = database.StringGet(prefixedKey);
                    if (hit.HasValue && ValueSerializer.TryDeserialize<T>(hit, out var cachedValue))
                    {
                        return cachedValue;
                    }
                    else
                    {
                        var freshValue = calculateValue();
                        var serializedValue = ValueSerializer.Serialize(freshValue);
                        var expiry = duration != TimeSpan.MaxValue ? duration : default(TimeSpan?);
                        database.StringSet(prefixedKey, serializedValue, expiry);
                        return freshValue;
                    }
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }

            // Fallback
            return calculateValue();
        }

        public async ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default)
        {
            if (wasTriggeredLocally && _redis.TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = KeyPrefix + key;
                    await database.KeyDeleteAsync(prefixedKey);
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }
        }

        public void Invalidate(string key, bool wasTriggeredLocally = true)
        {
            if (wasTriggeredLocally && _redis.TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = KeyPrefix + key;
                    database.KeyDelete(prefixedKey);
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }
        }
    }
}
