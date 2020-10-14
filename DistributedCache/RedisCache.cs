using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> based on Redis.
    /// </summary>
    public sealed class RedisCache : ICache, IHostedService
    {
        private volatile IConnectionMultiplexer? _redis;
        private readonly string _redisConnectionString;
        private readonly string _keyPrefix;
        private readonly IRedisValueSerializer _valueSerializer;
        private readonly Action<Exception> _exceptionLogger;
        private readonly Guid _clientId = Guid.NewGuid();

        public RedisCache(string redisConnectionString, string keyPrefix, IRedisValueSerializer valueSerializer, Action<Exception> exceptionLogger)
        {
            _redisConnectionString = redisConnectionString;
            _keyPrefix = keyPrefix;
            _valueSerializer = valueSerializer;
            _exceptionLogger = exceptionLogger;
        }

        private bool TryGetDatabase([NotNullWhen(true)] out IDatabase? database)
        {
            if (_redis is object && _redis.IsConnected)
            {
                database = _redis.GetDatabase();
                return true;
            }
            else
            {
                database = default;
                return false;
            }
        }

        private bool TryGetSubscriber([NotNullWhen(true)] out ISubscriber? database)
        {
            if (_redis is object && _redis.IsConnected)
            {
                database = _redis.GetSubscriber();
                return true;
            }
            else
            {
                database = default;
                return false;
            }
        }

        public async ValueTask<T> GetSet<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
        {
            if (TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = _keyPrefix + key;
                    var hit = await database.StringGetAsync(prefixedKey);
                    if (hit.HasValue && _valueSerializer.TryDeserialize<T>(hit, out var cachedValue))
                    {
                        return cachedValue;
                    }
                    else
                    {
                        var freshValue = await calculateValue(cancellationToken);
                        var serializedValue = _valueSerializer.Serialize(freshValue);
                        var expiry = duration != TimeSpan.MaxValue ? duration : default(TimeSpan?);
                        await database.StringSetAsync(prefixedKey, serializedValue, expiry);
                        await BroadcastInvalidatedKey(key);
                        return freshValue;
                    }
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }

            // Fallback
            return await calculateValue(cancellationToken);
        }

        public async ValueTask Invalidate(string key)
        {
            if (TryGetDatabase(out var database))
            {
                try
                {
                    var prefixedKey = _keyPrefix + key;
                    if (await database.KeyDeleteAsync(prefixedKey))
                    {
                        await BroadcastInvalidatedKey(key);
                    }
                }
                catch (RedisConnectionException)
                {
                    // Ignore this exception since we do not want to fail if redis is down.
                    // The connection error should have already been logged by who is managing the redis connection.
                }
            }
            KeyInvalidated?.Invoke(key);
        }

        public event Action<string>? KeyInvalidated;

        private const string InvalidationChannel = "cache-invalidation";

        private Task BroadcastInvalidatedKey(string key)
        {
            return TryGetSubscriber(out var subscriber)
                ? subscriber.PublishAsync(InvalidationChannel, new InvalidationMessage(_clientId, key))
                : Task.CompletedTask;
        }

        internal sealed class InvalidationMessage
        {
            private const int GuidLength = 16;

            public InvalidationMessage(Guid clientId, string key)
            {
                ClientId = clientId;
                Key = key;
            }

            public Guid ClientId { get; }
            public string Key { get; }

            private byte[] Encode()
            {
                var requiredLength = GuidLength + Encoding.UTF8.GetByteCount(Key);
                var data = new byte[requiredLength];

#if NETSTANDARD2_1
                ClientId.TryWriteBytes(data);
#else
                Array.Copy(ClientId.ToByteArray(), data, GuidLength);
#endif
                Encoding.UTF8.GetBytes(Key, 0, Key.Length, data, GuidLength);

                return data;
            }

            private static InvalidationMessage Decode(byte[] data)
            {
#if NETSTANDARD2_1
                var clientId = new Guid(data.AsSpan().Slice(0, GuidLength));
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
                var key = Encoding.UTF8.GetString(data, GuidLength, data.Length - GuidLength);
                return new InvalidationMessage(clientId, key);
            }

            public static implicit operator InvalidationMessage(RedisValue value) => Decode(value);

            public static implicit operator RedisValue(InvalidationMessage message) => message.Encode();
        }

        private async Task ProcessInvalidationSubscription(ISubscriber subscriber, CancellationToken cancellationToken)
        {
            var subscription = await subscriber.SubscribeAsync(InvalidationChannel);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var rawMessage = await subscription.ReadAsync(cancellationToken);
                    InvalidationMessage message = rawMessage.Message;

                    // Ignore my own messages
                    if (message.ClientId != this._clientId)
                    {
                        KeyInvalidated?.Invoke(message.Key);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _exceptionLogger(ex);
                }
            }

            await subscriber.UnsubscribeAsync(InvalidationChannel);
        }

        private readonly CancellationTokenSource _subscriptionCancellation = new CancellationTokenSource();
        private Task? _subscriptionProcessor;
        private Task? _initialConnection;

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            if (_initialConnection is object)
            {
                throw new InvalidOperationException("StartAsync has been called multiple times");
            }

            _initialConnection = Task.Run(async () =>
            {
                var options = ConfigurationOptions.Parse(_redisConnectionString);
                options.AbortOnConnectFail = false;

                _redis = await ConnectionMultiplexer.ConnectAsync(options);
                _redis.ConnectionFailed += (_, e) =>
                {
                    var exception = e.Exception ?? new RedisConnectionException(e.FailureType, "Connection to redis failed");
                    _exceptionLogger(exception);
                };

                _subscriptionProcessor = Task.Run(() => ProcessInvalidationSubscription(_redis.GetSubscriber(), _subscriptionCancellation.Token));
            });

            return Task.CompletedTask;
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _subscriptionCancellation.Cancel();
            if (_initialConnection is object)
            {
                await _initialConnection;
            }

            if (_redis is object)
            {
                await _redis.CloseAsync();
            }

            if (_subscriptionProcessor is object)
            {
                await _subscriptionProcessor;
            }
        }
    }
}
