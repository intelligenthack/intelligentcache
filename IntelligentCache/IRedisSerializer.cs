using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// Converts objects from / to a format that can be stored on Redis.
    /// </summary>
    public interface IRedisSerializer
    {
        /// <summary>
        /// Converts the specified parameter to a <see cref="RedisValue"/>.
        /// </summary>
        RedisValue Serialize<T>(T instance);

        /// <summary>
        /// Converts the specified value to an object of type <typeparamref name="T"/>.
        /// </summary>
        T? Deserialize<T>(RedisValue value);
    }
}
