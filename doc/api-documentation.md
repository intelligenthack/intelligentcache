# API documentation

## Reading a value from the cache

There is a single operation to read from the cache and compute the value in case of a miss, called `GetSet`. It takes a cache key, a callback and a duration. If the cache contains a value for the specified key, that value is returned immediately. Otherwise, the callback is invoked to compute the value, which is then stored into the cache for the specified duration.

In most cases, caching data is as simple as wrapping the existing code by a call to `GetSet`.

The following example shows how to get a value from the cache.

```c#
ICache cache = ...; // Get the cache from the DI container
string contentId = ...;
string cacheKey = "<some unique constructed key value, usually derived from contentId>";

var cachedValue = await cache.GetSet(cacheKey, async () =>
{
    // Read the value from the DB.
    // This callback will only be executed if the value was not found in the cache.
    using var sqlConnection = OpenConnection();
    return await sqlConnection.QueryFirstOrDefaultAsync<string>(
        "select Value from Content where Id = @contentId",
        new { contentId }
    );
}, 10); // Keep in cache for 10 seconds
```

The `GetSet` method has multiple overloads to allow different representations of the same parameters, which can be summarized as follows.

| Name                             | Description                                                                                                                                    |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `key`                            | The key used to lookup the value. This must uniquely identify the content and, in general, should be derived from the identifier of the value. |
| `calculateValue`                 | A callback that is invoked in case of a cache miss to calculate the value.                                                                     |
| `duration` / `durationInSeconds` | How long the value should be kept in the cache. If this parameter is omitted, the item never expires.                                          |
| `cancellationToken`              | An optional `CancellationToken` to cancel the asynchronous operations.                                                                         |

## Invalidating a value from the cache

When the source of cached data is modified, it may be desirable to invalidate the corresponding cache entry so that updated content is returned the next time it is requested. This is performed by calling the `Invalidate` method, as shown in the following example.

```c#
ICache cache = ...; // Get the cache from the DI container
string contentId = ...;
string newValue = ...;
string cacheKey = "<some unique constructed key value, usually derived from contentId>";

// Update the database
using var sqlConnection = OpenConnection();
await sqlConnection.ExecuteAsync(
    "update Content set Value = @newValue where Id = @contentId",
    new { newValue, contentId }
);

// Invalidate the cache
await cache.Invalidate(cacheKey);
```

The `Invalidate` method takes the following parameters:

| Name  | Description                                           |
| ----- | ----------------------------------------------------- |
| `key` | The key that was used previously to lookup the value. |

## MemoryCache

`MemoryCache` is an in-memory implementation that uses `System.Runtime.Caching.MemoryCache` as backing store.

```c#
var cache = new MemoryCache("exampleKey");
```

The `MemoryCache` constructor requires the following parameters:

| Name | Description |
|-|-|
| `prefix` | This prefix is used to compose the cache key to prevent collisions with other uses of `System.Runtime.Caching.MemoryCache`. A colon (`:`) character is always appended to this value. |

## RedisCache

`RedisCache` is a cache that uses [Redis](https://redis.io/) as backing store.

```c#
var connectionMultiplexer = ConnectionMultiplexer.Connect("example_redis_connection_string");
var cache = new RedisCache(connectionMultiplexer, "exampleKey");
```

The `RedisCache` constructor requires the following parameters:

| Name | Description |
|-|-|
| `redis` | An `IConnectionMultiplexer` that mediates access to Redis. |
| `prefix` | This prefix is used to compose the cache key to prevent collisions with other data stored in Redis. A colon (`:`) character is always appended to this value. |

### Custom serialization

Values stored on Redis need to be serialized. By default, they are serialized to JSON. This can be customized by setting the `Serializer` property to a different implementation of `IRedisSerializer`. This library provides an implementation that uses Protobuf. Following is an example of using that serializer.

```c#
var cache = new RedisCache(/* arguments */)
{
    Serializer = new ProtobufSerializer { CompressionFormat = CompressionFormat.Deflate }
};
```

## CompositeCache

`CompositeCache` composes two caches as a two-level hierarchy. When a lookup is performed, it gives priority to the first level, then only in case of miss is the second level checked. Since CompositeCache is itself a cache, more levels can be created if needed, as shown in the following example.

```c#
var multiLevelCache = new CompositeCache(
    level1Cache,
    new CompositeCache(
        level2cache,
        level3cache
    )
);
```

The `CompositeCache` constructor requires the following parameters:

| Name | Description |
|-|-|
| `level1` | The highest-priority level of the composite cache. |
| `level2` | The lowest-priority level of the composite cache. |

## RedisInvalidationSender and RedisInvalidationReceiver

This pair of classes implement cache invalidation across servers using Redis' pubsub mechanism.

`RedisInvalidationSender` acts as a cache that broadcasts an invalidation message every time a key is invalidated.

`RedisInvalidationReceiver` acts as a cache decorator that subscribes to invalidation messages and invalidates its inner cache when one is received.

The following code shows a possible composition of these components to implement a 2-level cache with a MemoryCache as first level and a RedisCache as second level.

```c#
ISubscriber subscriber = GetRedisSubscriber();
var invalidationChannel = RedisChannel.Literal("cache-invalidations");
var cache = new CompositeCache(
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
```

The `RedisInvalidationSender` constructor requires the following parameters:

| Name | Description |
|-|-|
| `subscriber` | An ISubscriber that allows publishing Redis pubsub messages. |
| `channel` | A `RedisChannel` where to publish invalidation messages. Use `RedisChannel.Literal("channel-name")` to create one. |

The `RedisInvalidationReceiver` constructor requires the following parameters:

| Name | Description |
|-|-|
| `inner` | The cache to invalidate. |
| `subscriber` | An ISubscriber that allows subscribing to Redis pubsub messages. |
| `channel` | A `RedisChannel` to subscribe invalidation messages from. Use `RedisChannel.Literal("channel-name")` to create one. |