using Newtonsoft.Json;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace IntelligentHack.DistributedCache
{
    public sealed class DefaultRedisValueSerializer : IRedisValueSerializer
    {
        private DefaultRedisValueSerializer() { }

        public static IRedisValueSerializer Instance = new DefaultRedisValueSerializer();

        public RedisValue Serialize(object? value)
        {
            return value switch
            {
                null => RedisValue.Null,
                int intValue => intValue,
                ulong ulongValue => ulongValue,
                double doubleValue => doubleValue,
                uint uintValue => uintValue,
                long longValue => longValue,
                bool boolValue => boolValue,
                string stringValue => stringValue,

                _ => JsonConvert.SerializeObject(value)
            };
        }

        [return: MaybeNull]
        public T Deserialize<T>(RedisValue serializedValue)
        {
            if (serializedValue.IsNull)
            {
                return default;
            }

            // This causes unnecessary boxing, but I could not find a more performant way to do this
            if (typeof(T) == typeof(int))
            {
                return (T)(object)(int)serializedValue;
            }
            else if (typeof(T) == typeof(ulong))
            {
                return (T)(object)(ulong)serializedValue;
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)(double)serializedValue;
            }
            else if (typeof(T) == typeof(uint))
            {
                return (T)(object)(uint)serializedValue;
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)(long)serializedValue;
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)(bool)serializedValue;
            }

            string serializedValueAsString = serializedValue;
            return typeof(T) == typeof(string)
                ? (T)(object)serializedValueAsString
                : JsonConvert.DeserializeObject<T>(serializedValueAsString);
        }
    }
}
