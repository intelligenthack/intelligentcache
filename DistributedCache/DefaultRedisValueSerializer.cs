using Newtonsoft.Json;
using StackExchange.Redis;
using System;

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

        public bool TryDeserialize<T>(RedisValue serializedValue, out T value)
        {
            try
            {
                if (serializedValue.IsNull)
                {
                    value = default!;
                }
                // This causes unnecessary boxing, but I could not find a more performant way to do this
                else if (typeof(T) == typeof(int))
                {
                    value = (T)(object)(int)serializedValue;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    value = (T)(object)(ulong)serializedValue;
                }
                else if (typeof(T) == typeof(double))
                {
                    value = (T)(object)(double)serializedValue;
                }
                else if (typeof(T) == typeof(uint))
                {
                    value = (T)(object)(uint)serializedValue;
                }
                else if (typeof(T) == typeof(long))
                {
                    value = (T)(object)(long)serializedValue;
                }
                else if (typeof(T) == typeof(bool))
                {
                    value = (T)(object)(bool)serializedValue;
                }
                else
                {
                    string serializedValueAsString = serializedValue;
                    value = typeof(T) == typeof(string)
                        ? (T)(object)serializedValueAsString
                        : JsonConvert.DeserializeObject<T>(serializedValueAsString);
                }
                return true;
            }
            catch (InvalidCastException)
            {
                value = default!;
                return false;
            }
            catch (JsonSerializationException)
            {
                value = default!;
                return false;
            }
        }
    }
}
