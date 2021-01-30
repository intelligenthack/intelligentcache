using Newtonsoft.Json;
using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache.Newtonsoft.Json
{
    /// <summary>
    /// An implementation of <see cref="IRedisSerializer" /> that encodes objects as JSON.
    /// </summary>
    public class JsonStringSerializer : IRedisSerializer
    {
        public T? Deserialize<T>(RedisValue value) => JsonConvert.DeserializeObject<T>(value);

        public RedisValue Serialize<T>(T instance) => JsonConvert.SerializeObject(instance);
    }
}
