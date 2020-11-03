using StackExchange.Redis;
using System;

namespace IntelligentHack.IntelligentCache
{
    public class CompositeSerializer : IRedisSerializer
    {
        private readonly IRedisSerializer _left;
        private readonly IRedisSerializer _right;

        public CompositeSerializer(IRedisSerializer left, IRedisSerializer right)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public T Deserialize<T>(RedisValue value)
        {
            return _right.Deserialize<T>(_left.Deserialize<RedisValue>(value));
        }

        public RedisValue Serialize<T>(T instance)
        {
            return _left.Serialize(_right.Serialize(instance));
        }
    }
}
