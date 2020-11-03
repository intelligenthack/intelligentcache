using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    public class RedisInvalidatorSender : ICache
    {
        private readonly ICache _inner;
        private readonly ISubscriber _subscriber;
        private readonly RedisChannel _channel;

        public RedisInvalidatorSender(ISubscriber subscriber, ICache inner, RedisChannel channel)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _channel = ((string)channel) ?? throw new ArgumentNullException(nameof(channel));
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
            _subscriber.Publish(_channel,key);
            _inner.Invalidate(key);
        }

        public async ValueTask InvalidateAsync(string key)
        {
            await _subscriber.PublishAsync(_channel,key);
            await _inner.InvalidateAsync(key);
        }
    }
}
