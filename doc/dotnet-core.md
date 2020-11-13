# Using with Asp.Net-Core

Register the cache as a services in your service collection by using the following code:

```
 services
    .AddSingleton(sp => new RedisCache(redisConnectionString))
    .AddHostedService(sp => sp.GetRequiredService<RedisCache>())
    .AddSingleton<ICache>(sp => new CompositeCache(
        level1: new MemoryCache(),
        level2: sp.GetRequiredService<RedisCache>()
    ));
```

Alternatively you can call the already provided extension method `AddRedisIntelligentCache`.

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisIntelligentCache(new RedisCache("localhost:6379"));
}
```

Even customize `RedisCache`:

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisIntelligentCache(new RedisCache("localhost:6379")
    {
        ExceptionLogger = ex => Console.WriteLine(ex),
        KeyPrefix = ":mychache",
        ValueSerializer = new MyRedisValueSerializerImplementacion()
    });
}
```

## Customizing Redis cache

It is possible to customize the serialization, exception logs and key prefix appended to all keys that are stored on Redis.

### Custom serialization

Serialization can be customized by setting an implementation of `IRedisValueSerializer` to the `ValueSerializer` property on `RedisCache` class.

### Custom exception logs

Exception logs can be changed by providing a new action for `ExceptionLogger` property. By default all exception will be logged in the `Console`.

### Custom key prefix

A new key prefix of your choice can be applied by assigning it to `KeyPrefix` property .
