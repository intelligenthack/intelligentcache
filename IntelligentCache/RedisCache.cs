using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that stores values on Redis.
    /// </summary>
    public class RedisCache : ICache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _prefix;

        public IRedisSerializer Serializer { get; set; } = new SystemTextJsonSerializer();

        /// <param name="redis">An IConnectionMultiplexer that mediates access to Redis.</param>
        /// <param name="prefix">A prefix that is inserted before each key to prevent collisions with other users of Redis.</param>
        public RedisCache(IConnectionMultiplexer redis, string prefix)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));

            if (prefix is null) throw new ArgumentNullException(nameof(prefix));

            _prefix = $"{prefix}:";
        }

        public async Task<T?> GetSetAsync<T>(string key, Func<CancellationToken, Task<T?>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var k = $"{_prefix}{key}";
            var db = _redis.GetDatabase();
            
            var value = await db.StringGetAsync(k).ConfigureAwait(false);

            if (!value.IsNull)
                return Serializer.Deserialize<T>(value.ToString());
            
            var res = await calculateValue(cancellationToken).ConfigureAwait(false);
            await db.StringSetAsync(k, Serializer.Serialize(res), duration).ConfigureAwait(false);
            
            return res;
        }

        public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            await db.StringSetAsync(k, RedisValue.Null).ConfigureAwait(false);
        }

        public T? GetSet<T>(string key, Func<T?> calculateValue, TimeSpan duration) where T : class
        {
            var k = $"{_prefix}{key}";
            var db = _redis.GetDatabase();
            
            var value = db.StringGet(k);

            if (!value.IsNull)
                return Serializer.Deserialize<T>(value.ToString());
            
            var res = calculateValue();
            db.StringSet(k, Serializer.Serialize(res), duration);
            return res;
        }

        public void Invalidate(string key) => _redis.GetDatabase().StringSet($"{_prefix}{key}", RedisValue.Null);
    }
}
