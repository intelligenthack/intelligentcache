using System.Text.Json;
using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="IRedisSerializer" /> that encodes objects as JSON.
    /// </summary>
    public class SystemTextJsonSerializer : IRedisSerializer
    {
        public T? Deserialize<T>(RedisValue value) => JsonSerializer.Deserialize<T>(value);

        public RedisValue Serialize<T>(T instance) => JsonSerializer.SerializeToUtf8Bytes(instance);
    }
}
