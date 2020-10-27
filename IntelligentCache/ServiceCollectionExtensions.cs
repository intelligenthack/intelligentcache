using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

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
            RedisCache redisImplementation)
        { 
            return services
                .AddSingleton(sp => redisImplementation)
                .AddHostedService(sp => sp.GetRequiredService<RedisCache>())
                .AddSingleton<ICache>(sp => new CompositeCache(
                    level1: new MemoryCache(),
                    level2: sp.GetRequiredService<RedisCache>()
                ));
        }
    }
}
