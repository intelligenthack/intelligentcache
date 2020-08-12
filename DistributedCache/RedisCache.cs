using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> based on Redis.
    /// </summary>
    public sealed class RedisCache : ICache, IHostedService
    {
        private IConnectionMultiplexer? _redis;
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

        private IConnectionMultiplexer Redis => _redis ?? throw new InvalidOperationException("IHostedService.StartAsync has not been called yet");
        private IDatabase Database => Redis.GetDatabase();
        private ISubscriber Subscriber => Redis.GetSubscriber();

        public async ValueTask<T> GetSetAsync<T>(string key, Func<ValueTask<T>> calculateValue, TimeSpan duration)
        {
            if (Redis.IsConnected)
            {
                try
                {
                    var prefixedKey = _keyPrefix + key;
                    var hit = await Database.StringGetAsync(prefixedKey);
                    if (hit.HasValue)
                    {
                        return _valueSerializer.Deserialize<T>(hit);
                    }
                    else
                    {
                        var freshValue = await calculateValue();
                        var serializedValue = _valueSerializer.Serialize(freshValue);
                        var expiry = duration != TimeSpan.MaxValue ? duration : default(TimeSpan?);
                        await Database.StringSetAsync(prefixedKey, serializedValue, expiry);
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
            return await calculateValue();
        }

        public async ValueTask Invalidate(string key)
        {
            if (Redis.IsConnected)
            {
                try
                {
                    var prefixedKey = _keyPrefix + key;
                    if (await Database.KeyDeleteAsync(prefixedKey))
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
        }

        public event Action<string>? KeyInvalidated;

        private const string InvalidationChannel = "cache-invalidation";

        private Task BroadcastInvalidatedKey(string key)
        {
            return Subscriber.PublishAsync(InvalidationChannel, new InvalidationMessage(_clientId, key).ToString());
        }

        private sealed class InvalidationMessage
        {
            private static readonly int GuidStringLength = Guid.Empty.ToString().Length;

            public InvalidationMessage(Guid clientId, string key)
            {
                ClientId = clientId;
                Key = key;
            }

            public Guid ClientId { get; }
            public string Key { get; }

            public static InvalidationMessage Parse(string serializedMessage)
            {
                return new InvalidationMessage(
                    Guid.Parse(serializedMessage.Substring(0, GuidStringLength)),
                    serializedMessage.Substring(GuidStringLength)
                );
            }

            public override string ToString() => ClientId.ToString() + Key;
        }

        private async Task ProcessInvalidationSubscription(CancellationToken cancellationToken)
        {
            var subscription = await Subscriber.SubscribeAsync(InvalidationChannel);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var rawMessage = await subscription.ReadAsync(cancellationToken);
                    var message = InvalidationMessage.Parse(rawMessage.Message);
                    
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
        }

        private readonly CancellationTokenSource _subscriptionCancellation = new CancellationTokenSource();
        private Task? _subscriptionProcessor;

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            var options = ConfigurationOptions.Parse(_redisConnectionString);
            options.AbortOnConnectFail = false;

            _redis = await ConnectionMultiplexer.ConnectAsync(options);
            _redis.ConnectionFailed += (_, e) =>
            {
                var exception = e.Exception ?? new RedisConnectionException(e.FailureType, "Connection to redis failed");
                _exceptionLogger(exception);
            };

            _subscriptionProcessor = Task.Run(() => ProcessInvalidationSubscription(_subscriptionCancellation.Token));
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _subscriptionCancellation.Cancel();

            if (_redis != null)
            {
                await _redis.CloseAsync();
            }

            if (_subscriptionProcessor != null)
            {
                await _subscriptionProcessor;
            }
        }
    }
}
