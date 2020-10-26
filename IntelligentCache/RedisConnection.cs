using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    public sealed class RedisConnection : IHostedService
    {
        private readonly CancellationTokenSource _subscriptionCancellation = new CancellationTokenSource();
        private readonly string _redisConnectionString;
        private volatile IConnectionMultiplexer? _redis;
        private readonly List<Task> _subscriptionProcessors = new List<Task>();
        private Task? _initialConnection;
        private readonly TaskCompletionSource<ISubscriber> _subscriber = new TaskCompletionSource<ISubscriber>();

        public Action<Exception> ExceptionLogger { get; set; }

        public RedisConnection(string redisConnectionString)
        {
            _redisConnectionString = redisConnectionString;
            ExceptionLogger = RedisOptions.DefaultExceptionLogger;
        }

        public bool TryGetDatabase([NotNullWhen(true)] out IDatabase? database)
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

        public bool TryGetSubscriber([NotNullWhen(true)] out ISubscriber? subscriber)
        {
            if (_redis is object && _redis.IsConnected)
            {
                subscriber = _redis.GetSubscriber();
                return true;
            }
            else
            {
                subscriber = default;
                return false;
            }
        }

        public void Subscribe(string channel, Func<RedisValue, CancellationToken, Task> processMessage)
        {
            if (_initialConnection is object)
            {
                throw new InvalidOperationException("Subscribe must be called before StartAsync");
            }

            _subscriptionProcessors.Add(Task.Run(async () =>
            {
                var subscriber = await _subscriber.Task;

                var cancellationToken = _subscriptionCancellation.Token;
                var subscription = await subscriber.SubscribeAsync(channel);
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var rawMessage = await subscription.ReadAsync(cancellationToken);
                        await processMessage(rawMessage.Message, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        ExceptionLogger(ex);
                    }
                }

                await subscriber.UnsubscribeAsync(channel);
            }));
        }

        public Task StartAsync(CancellationToken cancellationToken)
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
                    ExceptionLogger(exception);
                };

                _subscriber.SetResult(_redis.GetSubscriber());
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
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

            await Task.WhenAll(_subscriptionProcessors);
        }
    }
}
