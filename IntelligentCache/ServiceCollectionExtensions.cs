using Microsoft.Extensions.DependencyInjection;
using System;

namespace IntelligentHack.IntelligentCache
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an <see cref="ICache" /> that implements a distributed cache backed on Redis.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnectionString">The redis connection string.</param>
        public static IServiceCollection AddRedisIntelligentCache(
            this IServiceCollection services,
            string redisConnectionString,
            Action<RedisOptions>? configure = null)
        {
            var options = new RedisOptions();
            configure?.Invoke(options);

            var redis = new RedisConnection(redisConnectionString)
            {
                ExceptionLogger = options.ExceptionLogger,
            };

            var cache = new RedisInvalidationPropagator(
                new CompositeCache(
                    level1: new MemoryCache(),
                    level2: new RedisCache(redis)
                    {
                        KeyPrefix = options.KeyPrefix,
                        ValueSerializer = options.ValueSerializer,
                    }
                ),
                redis,
                options.InvalidationChannel
            );

            return services
                .AddSingleton<ICache>(sp => cache)
                .AddHostedService(sp => redis);
        }
    }

    public class RedisOptions
    {
        public static void DefaultExceptionLogger(Exception ex) => Console.Error.WriteLine(ex);

        public Action<Exception> ExceptionLogger { get; set; } = DefaultExceptionLogger;
        public string KeyPrefix { get; set; } = RedisCache.DefaultKeyPrefix;
        public IRedisValueSerializer ValueSerializer { get; set; } = DefaultRedisValueSerializer.Instance;
        public string InvalidationChannel { get; set; } = RedisInvalidationPropagator.DefaultInvalidationChannel;
    }
}
