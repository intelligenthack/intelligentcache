using StackExchange.Redis;
using System;
using ProtoBuf;
using System.IO;

namespace IntelligentHack.IntelligentCache
{
    public class ProtobufSerializer : IRedisSerializer
    {
        public T Deserialize<T>(RedisValue value)
        {
            return Serializer.Deserialize<T>(value);
        }

        public RedisValue Serialize<T>(T instance)
        {
            using(var memStream = new MemoryStream())
            {
                Serializer.Serialize(memStream, instance);
                return RedisValue.CreateFrom(memStream);
            }
        }
    }
}
