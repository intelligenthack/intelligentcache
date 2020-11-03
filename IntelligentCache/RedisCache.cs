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

        public TimeSpan CacheDuration { get; set; }

        public IRedisSerializer Serializer { get; set; } = new JsonStringSerializer();

        public RedisCache(IConnectionMultiplexer redis, string prefix = "")
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _prefix = prefix + ":";
            this.CacheDuration = TimeSpan.FromHours(1);
        }
        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, CancellationToken cancellationToken = default) where T : class
        {
            return await this.GetSetAsync(key,calculateValue,this.CacheDuration,cancellationToken);
        }

        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            var value = await db.StringGetAsync(k);

            if (value.IsNull)
            {
                var res = await calculateValue(cancellationToken);
                await db.StringSetAsync(k, Serializer.Serialize(res), duration);
                return res;
            }

            return Serializer.Deserialize<T>(value.ToString());
        }

        public async ValueTask InvalidateAsync(string key)
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            await db.StringSetAsync(k, RedisValue.Null);
        }
        public T GetSet<T>(string key, Func<T> calculateValue) where T : class
        {
            return this.GetSet(key,calculateValue,this.CacheDuration);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            var db = _redis.GetDatabase();
            var k = _prefix + key;
            var value = db.StringGet(k);

            if (value.IsNull)
            {
                var res = calculateValue() ?? throw new ArgumentNullException(nameof(calculateValue));
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
