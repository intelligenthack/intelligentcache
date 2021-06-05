#pragma warning disable RCS1102 // Make class static.

using FakeItEasy;
using StackExchange.Redis;
using System;
using System.Collections.Generic;

namespace IntelligentCache.Tests
{
    internal class FakeRedis
    {
        public static IConnectionMultiplexer CreateConnectionMultiplexer(Func<RedisKey, RedisValue> onGet = null, Action<RedisKey, RedisValue, TimeSpan?> onSet = null)
        {
            var multiplexer = A.Fake<IConnectionMultiplexer>(o => o.Strict());
            var database = A.Fake<IDatabase>(o => o.Strict());

            A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);

            A.CallTo(() => database.StringGet(A<RedisKey>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, CommandFlags _) => onGet?.Invoke(key) ?? RedisValue.Null);

            A.CallTo(() => database.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, CommandFlags _) => onGet?.Invoke(key) ?? RedisValue.Null);

            A.CallTo(() => database.StringSet(A<RedisKey>._, A<RedisValue>._, A<TimeSpan?>._, A<When>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, TimeSpan? expiry, When _, CommandFlags __) =>
                {
                    onSet?.Invoke(key, value, expiry);
                    return true;
                });

            A.CallTo(() => database.StringSetAsync(A<RedisKey>._, A<RedisValue>._, A<TimeSpan?>._, A<When>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, TimeSpan? expiry, When _, CommandFlags __) =>
                {
                    onSet?.Invoke(key, value, expiry);
                    return true;
                });

            return multiplexer;
        }

        public static ISubscriber CreateSubscriber(Action<RedisChannel, RedisValue> onPublish = null)
        {
            var subscriber = A.Fake<ISubscriber>();

            var subscriptions = new List<Action<RedisChannel, RedisValue>>();

            A.CallTo(() => subscriber.Subscribe(A<RedisChannel>._, A<Action<RedisChannel, RedisValue>>._, A<CommandFlags>._))
                .Invokes((RedisChannel _, Action<RedisChannel, RedisValue> handler, CommandFlags __) => subscriptions.Add(handler));

            A.CallTo(() => subscriber.Publish(A<RedisChannel>._, A<RedisValue>._, A<CommandFlags>._))
                .Invokes((RedisChannel channel, RedisValue message, CommandFlags _) => PublishHandler(channel, message));

            A.CallTo(() => subscriber.PublishAsync(A<RedisChannel>._, A<RedisValue>._, A<CommandFlags>._))
                .Invokes((RedisChannel channel, RedisValue message, CommandFlags _) => PublishHandler(channel, message));

            void PublishHandler(RedisChannel channel, RedisValue message)
            {
                onPublish?.Invoke(channel, message);

                foreach (var handler in subscriptions!)
                {
                    handler(channel, message);
                }
            }

            return subscriber;
        }
    }
}
