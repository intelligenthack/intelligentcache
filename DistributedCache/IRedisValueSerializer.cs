using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace IntelligentHack.DistributedCache
{
    public interface IRedisValueSerializer
    {
        RedisValue Serialize(object? value);
        bool TryDeserialize<T>(RedisValue serializedValue, out T value);
    }
}
