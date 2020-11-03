﻿using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    public class RedisInvalidatorReceiver : ICache
    {
        private readonly ICache _inner;
        private readonly ISubscriber _subscriber;

        public RedisInvalidatorReceiver(ISubscriber subscriber, ICache inner, RedisChannel channel)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _ = ((string)channel) ?? throw new ArgumentNullException(nameof(channel));
            _subscriber.Subscribe(channel, Pulse);
        }

        private void Pulse(RedisChannel channel, RedisValue value)
        {
            _inner.Invalidate(value);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return _inner.GetSet(key, calculateValue, duration);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return _inner.GetSetAsync(key, calculateValue, duration);
        }

        public void Invalidate(string key)
        {
            _inner.Invalidate(key);
        }

        public ValueTask InvalidateAsync(string key)
        {
            return _inner.InvalidateAsync(key);
        }
    }
}