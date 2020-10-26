using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache
{
    public interface IRedisValueSerializer
    {
        RedisValue Serialize(object? value);
        bool TryDeserialize<T>(RedisValue serializedValue, out T value);
    }
}
