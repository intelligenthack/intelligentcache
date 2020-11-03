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
        public TimeSpan CacheDuration { get; set; }

        public RedisInvalidatorSender(ISubscriber subscriber, RedisChannel channel)
        {
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _channel = ((string)channel) ?? throw new ArgumentNullException(nameof(channel));
            this.CacheDuration = TimeSpan.FromHours(1);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return calculateValue() ?? throw new ArgumentNullException(nameof(calculateValue));
        }

        public T GetSet<T>(string key, Func<T> calculateValue) where T : class
        {
            return this.GetSet(key,calculateValue,this.CacheDuration);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return calculateValue(cancellationToken);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, CancellationToken cancellationToken = default) where T : class
        {
            return this.GetSetAsync(key,calculateValue,this.CacheDuration,cancellationToken);
        }

        public void Invalidate(string key)
        {
            _subscriber.Publish(_channel,key);
        }

        public async ValueTask InvalidateAsync(string key)
        {
            await _subscriber.PublishAsync(_channel,key);
        }
    }
}
