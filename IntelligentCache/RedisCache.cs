using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{

    public class RedisCache : ICache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _prefix;

        public IRedisSerializer Serializer { get; set; } = new JsonStringSerializer();

        public RedisCache(IConnectionMultiplexer redis, string prefix = "")
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _prefix = prefix + ":";
        }

        public async Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            var value = await db.StringGetAsync(k).ConfigureAwait(false);

            if (value.IsNull)
            {
                var res = await calculateValue(cancellationToken).ConfigureAwait(false);
                await db.StringSetAsync(k, Serializer.Serialize(res), duration).ConfigureAwait(false);
                return res;
            }

            return Serializer.Deserialize<T>(value.ToString());
        }

        public async Task InvalidateAsync(string key)
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            await db.StringSetAsync(k, RedisValue.Null).ConfigureAwait(false);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            var value = db.StringGet(k);

            if (value.IsNull)
            {
                var res = calculateValue();
                db.StringSet(k, Serializer.Serialize(res), duration);
                return res;
            }

            return Serializer.Deserialize<T>(value.ToString());
        }

        public void Invalidate(string key)
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            db.StringSet(k, RedisValue.Null);
        }
    }
}
