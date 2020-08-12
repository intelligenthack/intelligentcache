using StackExchange.Redis;

namespace IntelligentHack.DistributedCache
{
    public interface IRedisValueSerializer
    {
        RedisValue Serialize(object? value);
        T Deserialize<T>(RedisValue serializedValue);
    }
}
