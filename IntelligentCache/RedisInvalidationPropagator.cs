using StackExchange.Redis;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An invalidation propagator for caches based on Redis communication mechanism.
    /// </summary>
    public sealed class RedisInvalidationPropagator : ICache
    {
        private readonly ICache _inner;
        private readonly RedisConnection _redis;
        private readonly string _invalidationChannel;
        private readonly Guid _clientId = Guid.NewGuid();

        public const string DefaultInvalidationChannel = "cache-invalidation";

        public RedisInvalidationPropagator(ICache inner, RedisConnection redis, string invalidationChannel = DefaultInvalidationChannel)
        {
            _inner = inner;
            _redis = redis;
            _invalidationChannel = invalidationChannel;

            redis.Subscribe(invalidationChannel, ProcessInvalidationMessage);
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValueAsync, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return _inner.GetSetAsync(key, calculateValueAsync, duration, cancellationToken);
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
        {
            return _inner.GetSet(key, calculateValue, duration);
        }

        public async ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default)
        {
            await _inner.InvalidateAsync(key, wasTriggeredLocally, cancellationToken);
            try
            {
                if (wasTriggeredLocally && _redis.TryGetSubscriber(out var subscriber))
                {
                    await subscriber.PublishAsync(_invalidationChannel, new InvalidationMessage(_clientId, key));
                }
            }
            catch (RedisConnectionException)
            {
                // Ignore this exception since we do not want to fail if redis is down.
                // The connection error should have already been logged by who is managing the redis connection.
            }
        }

        public void Invalidate(string key, bool wasTriggeredLocally = true)
        {
            _inner.Invalidate(key, wasTriggeredLocally);
            try
            {
                if (wasTriggeredLocally && _redis.TryGetSubscriber(out var subscriber))
                {
                    subscriber.Publish(_invalidationChannel, new InvalidationMessage(_clientId, key));
                }
            }
            catch (RedisConnectionException)
            {
                // Ignore this exception since we do not want to fail if redis is down.
                // The connection error should have already been logged by who is managing the redis connection.
            }
        }

        private async Task ProcessInvalidationMessage(RedisValue rawMessage, CancellationToken cancellationToken)
        {
            InvalidationMessage message = rawMessage;

            // Ignore my own messages
            if (message.ClientId != this._clientId)
            {
                await _inner.InvalidateAsync(message.Key, false, cancellationToken);
            }
        }

        internal sealed class InvalidationMessage
        {
            private const int _guidLength = 16;

            public InvalidationMessage(Guid clientId, string key)
            {
                ClientId = clientId;
                Key = key;
            }

            public Guid ClientId { get; }
            public string Key { get; }

            private byte[] Encode()
            {
                var requiredLength = _guidLength + Encoding.UTF8.GetByteCount(Key);
                var data = new byte[requiredLength];

#if NETSTANDARD2_1
                ClientId.TryWriteBytes(data);
#else
                Array.Copy(ClientId.ToByteArray(), data, _guidLength);
#endif
                Encoding.UTF8.GetBytes(Key, 0, Key.Length, data, _guidLength);

                return data;
            }

            private static InvalidationMessage Decode(byte[] data)
            {
#if NETSTANDARD2_1
                var clientId = new Guid(data.AsSpan().Slice(0, _guidLength));
#else
                var clientId = new Guid(
                    BitConverter.ToUInt32(data, 0),
                    BitConverter.ToUInt16(data, 4),
                    BitConverter.ToUInt16(data, 6),
                    data[8],
                    data[9],
                    data[10],
                    data[11],
                    data[12],
                    data[13],
                    data[14],
                    data[15]
                );
#endif
                var key = Encoding.UTF8.GetString(data, _guidLength, data.Length - _guidLength);
                return new InvalidationMessage(clientId, key);
            }

            public static implicit operator InvalidationMessage(RedisValue value) => Decode(value);

            public static implicit operator RedisValue(InvalidationMessage message) => message.Encode();
        }
    }
}
