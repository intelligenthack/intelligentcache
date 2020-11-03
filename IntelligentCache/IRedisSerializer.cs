using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache
{
    public interface IRedisSerializer
    {
        RedisValue Serialize<T>(T instance);
        T Deserialize<T>(RedisValue value);
    }
}
