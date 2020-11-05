using FakeItEasy;
using StackExchange.Redis;
using System;
using System.Collections.Generic;

namespace IntelligentCache.Tests
{
    internal class FakeRedisTopic
    {
        public ISubscriber Subscriber { get; }

        private readonly List<Action<RedisChannel, RedisValue>> _handlers = new List<Action<RedisChannel, RedisValue>>();

        public FakeRedisTopic()
        {
            Subscriber = A.Fake<ISubscriber>();

            A.CallTo(() => Subscriber.Subscribe(A<RedisChannel>._, A<Action<RedisChannel, RedisValue>>._, A<CommandFlags>._))
                .Invokes((RedisChannel channel, Action<RedisChannel, RedisValue> handler, CommandFlags flags) =>
                {
                    _handlers.Add(handler);
                });
        }

        public void Publish(RedisChannel channel, RedisValue value)
        {
            foreach (var handler in _handlers)
            {
                handler(channel, value);
            }
        }
    }
}
