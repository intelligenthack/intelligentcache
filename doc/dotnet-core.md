# Using with Asp.Net-Core

Register the cache as a services in your service collection by using the following code:

```c#
services
    .AddSingleton<ICache>(sp => {
        ISubscriber subscriber = GetRedisSubscriber();
        var invalidationChannel = "cache-invalidations";
        return new CompositeCache(
            new RedisInvalidationReceiver(
                new MemoryCache(/* arguments */),
                subscriber,
                invalidationChannel
            ),
            new CompositeCache(
                new RedisCache(/* arguments */),
                new RedisInvalidationSender(subscriber, invalidationChannel)
            )
        );
    });
```
