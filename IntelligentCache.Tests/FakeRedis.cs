using FakeItEasy;
using StackExchange.Redis;
using System;

namespace IntelligentCache.Tests
{
    internal class FakeRedis
    {
        internal static IConnectionMultiplexer Create(Func<RedisKey, RedisValue>? onGet = null, Action<RedisKey, RedisValue, TimeSpan?>? onSet = null)
        {
            var multiplexer = A.Fake<IConnectionMultiplexer>(o => o.Strict());
            var database = A.Fake<IDatabase>(o => o.Strict());

            A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);

            A.CallTo(() => database.StringGet(A<RedisKey>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, CommandFlags flags) => onGet?.Invoke(key) ?? RedisValue.Null);

            A.CallTo(() => database.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, CommandFlags flags) => onGet?.Invoke(key) ?? RedisValue.Null);

            A.CallTo(() => database.StringSet(A<RedisKey>._, A<RedisValue>._, A<TimeSpan?>._, A<When>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags) =>
                {
                    onSet?.Invoke(key, value, expiry);
                    return true;
                });

            A.CallTo(() => database.StringSetAsync(A<RedisKey>._, A<RedisValue>._, A<TimeSpan?>._, A<When>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags) =>
                {
                    onSet?.Invoke(key, value, expiry);
                    return true;
                });

            return multiplexer;
        }
    }
}
