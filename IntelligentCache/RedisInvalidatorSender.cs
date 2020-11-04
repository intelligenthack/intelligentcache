using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    public class RedisInvalidatorSender : ICache
    {
        private readonly ISubscriber _subscriber;
        private readonly RedisChannel _channel;

        public RedisInvalidatorSender(ISubscriber subscriber, RedisChannel channel)
        {
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _channel = ((string)channel) ?? throw new ArgumentNullException(nameof(channel));
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return calculateValue();
        }

        public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return calculateValue(cancellationToken);
        }

        public void Invalidate(string key)
        {
            _subscriber.Publish(_channel,key);
        }

        public Task InvalidateAsync(string key)
        {
            return _subscriber.PublishAsync(_channel,key);
        }
    }
}
