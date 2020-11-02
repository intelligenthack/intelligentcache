using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> based on Redis.
    /// </summary>

    public interface IStringSerializer
    {
        string Serialize<T>(T instance);
        T Deserialize<T>(string value);
    }

    public class RedisCache : ICache
    {
        public class JsonStringSerializer : IStringSerializer
        {
            public T Deserialize<T>(string value)
            {
                return JsonConvert.DeserializeObject<T>(value);
            }

            public string Serialize<T>(T instance)
            {
                return JsonConvert.SerializeObject(instance);
            }
        }

        private IConnectionMultiplexer? _redis;
        string _prefix;

        public IStringSerializer Serializer { get; set; } = new JsonStringSerializer();


        public RedisCache(IConnectionMultiplexer redis, string prefix = "")
        {
            if (redis == null) throw new ArgumentNullException(nameof(redis));
            _redis = redis;
            _prefix = prefix;
        }

        public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default)
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

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
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
            db.StringSetAsync(k, RedisValue.Null);
        }

    }
}
