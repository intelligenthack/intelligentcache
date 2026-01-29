using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// Publishes invalidation messages to a Redis topic when invalidated.
    /// </summary>
    public class RedisInvalidationSender : ICache
    {
        private readonly ISubscriber _subscriber;
        private readonly RedisChannel _channel;

        /// <param name="subscriber">An ISubscriber that allows publishing Redis pubsub messages.</param>
        /// <param name="channel">The channel that invalidation messages are published to.</param>
        public RedisInvalidationSender(ISubscriber subscriber, RedisChannel channel)
        {
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            if (channel.IsNullOrEmpty) throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            _channel = channel;
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
            _subscriber.Publish(_channel, key);
        }

        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            return _subscriber.PublishAsync(_channel, key);
        }
    }
}
