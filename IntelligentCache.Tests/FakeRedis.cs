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

            A.CallTo(() => database.StringSet(A<RedisKey>._, A<RedisValue>._, A<Expiration>._, A<ValueCondition>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, Expiration expiry, ValueCondition _, CommandFlags __) =>
                {
                    onSet?.Invoke(key, value, ExpirationToTimeSpan(expiry));
                    return true;
                });

            A.CallTo(() => database.StringSetAsync(A<RedisKey>._, A<RedisValue>._, A<Expiration>._, A<ValueCondition>._, A<CommandFlags>._))
                .ReturnsLazily((RedisKey key, RedisValue value, Expiration expiry, ValueCondition _, CommandFlags __) =>
                {
                    onSet?.Invoke(key, value, ExpirationToTimeSpan(expiry));
                    return true;
                });

            return multiplexer;
        }

        private static TimeSpan? ExpirationToTimeSpan(Expiration expiry)
        {
            // Parse from ToString() output like "EX 10" (seconds) or "PX 1000" (milliseconds) or empty
            var str = expiry.ToString();
            if (string.IsNullOrEmpty(str)) return null;

            var parts = str.Split(' ');
            if (parts.Length != 2 || !long.TryParse(parts[1], out var value)) return null;

            return parts[0] switch
            {
                "EX" => TimeSpan.FromSeconds(value),
                "PX" => TimeSpan.FromMilliseconds(value),
                _ => null
            };
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
