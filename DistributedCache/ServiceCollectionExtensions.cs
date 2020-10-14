using Microsoft.Extensions.DependencyInjection;
using System;

namespace IntelligentHack.DistributedCache
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an <see cref="ICache" /> that implements a distributed cache backed on Redis.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnectionString">The redis connection string.</param>
        /// <param name="exceptionLogger">A callback to log exceptions found in background tasks.</param>
        /// <param name="valueSerializer">An <see cref="IRedisValueSerializer"/> used to convert values from and to string.</param>
        /// <param name="redisKeyPrefix">A prefix that is appended to the keys on redis, to avoid naming collisions.</param>
        public static IServiceCollection AddRedisDistributedCache(this IServiceCollection services, string redisConnectionString, Action<Exception> exceptionLogger, IRedisValueSerializer? valueSerializer = null, string redisKeyPrefix = "cache:")
        {
            return services
                .AddSingleton(sp => new RedisCache(redisConnectionString, redisKeyPrefix, valueSerializer ?? DefaultRedisValueSerializer.Instance, exceptionLogger))
                .AddHostedService(sp => sp.GetRequiredService<RedisCache>())
                .AddSingleton<ICache>(sp => new CompositeCache(
                    level1: new MemoryCache(WallClock.Instance),
                    level2: sp.GetRequiredService<RedisCache>()
                ));
        }
    }
}
