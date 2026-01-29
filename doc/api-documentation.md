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

## Null Value Handling

When `calculateValue` returns `null`, the value is **not cached** and `null` is returned to the caller. This is by design:

```c#
var result = cache.GetSet("key", () =>
{
    var item = database.FindById(id);
    return item; // If null, nothing is cached
}, TimeSpan.FromMinutes(5));

// result is null, and subsequent calls will invoke calculateValue again
```

### Why null is not cached

1. **Ambiguity**: A cached `null` is indistinguishable from a cache miss
2. **Negative caching risks**: Caching "not found" results can cause issues when data is created
3. **Storage limitations**: Some cache backends don't support null values

### Caching "not found" results

If you need to cache negative results (e.g., to avoid repeated database lookups for non-existent items), use a wrapper type:

```c#
public class CacheResult<T> where T : class
{
    public T Value { get; set; }
    public bool Found { get; set; }

    public static CacheResult<T> NotFound() => new() { Found = false };
    public static CacheResult<T> Of(T value) => new() { Value = value, Found = true };
}

// Usage
var result = cache.GetSet("user:123", () =>
{
    var user = database.FindUser(123);
    return user != null
        ? CacheResult<User>.Of(user)
        : CacheResult<User>.NotFound();
}, TimeSpan.FromMinutes(5));

if (result.Found)
{
    // Use result.Value
}
else
{
    // User doesn't exist (and this fact is now cached)
}
```

### Null keys

Cache keys can technically be `null` or empty, but this is not recommended. The key will be prefixed (e.g., `"myapp:null"` or `"myapp:"`), which may cause unexpected collisions. Always use meaningful, non-empty keys.

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

### Serialization

Values stored on Redis need to be serialized. The `Serializer` property controls how objects are converted to and from Redis values.

#### JsonStringSerializer (Default)

By default, `RedisCache` uses `JsonStringSerializer`, which serializes objects to JSON using [Json.NET](https://www.newtonsoft.com/json):

```c#
var cache = new RedisCache(redis, "myapp");
// Equivalent to:
var cache = new RedisCache(redis, "myapp") { Serializer = new JsonStringSerializer() };
```

JSON serialization is human-readable and works with any serializable type, but produces larger payloads than binary formats.

#### ProtobufSerializer

For better performance and smaller payloads, use `ProtobufSerializer` which uses [protobuf-net](https://github.com/protobuf-net/protobuf-net):

```c#
var cache = new RedisCache(redis, "myapp")
{
    Serializer = new ProtobufSerializer()
};
```

**Important:** Types must be decorated with protobuf-net attributes:

```c#
[ProtoContract]
public class Product
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; }
}
```

#### Compression Options

`ProtobufSerializer` supports optional compression via the `CompressionFormat` property:

| CompressionFormat | Description |
|-|-|
| `CompressionFormat.GZip` | GZip compression (default). Good balance of speed and compression ratio. |
| `CompressionFormat.Deflate` | Deflate compression. Slightly faster than GZip, similar compression. |
| `CompressionFormat.None` | No compression. Fastest, but larger payloads. |

```c#
// With GZip compression (default)
var cache = new RedisCache(redis, "myapp")
{
    Serializer = new ProtobufSerializer { CompressionFormat = CompressionFormat.GZip }
};

// With Deflate compression
var cache = new RedisCache(redis, "myapp")
{
    Serializer = new ProtobufSerializer { CompressionFormat = CompressionFormat.Deflate }
};

// Without compression
var cache = new RedisCache(redis, "myapp")
{
    Serializer = new ProtobufSerializer { CompressionFormat = CompressionFormat.None }
};
```

#### Custom Serializers

You can implement your own serializer by implementing `IRedisSerializer`:

```c#
public interface IRedisSerializer
{
    RedisValue Serialize<T>(T instance);
    T Deserialize<T>(RedisValue value);
}
```

Example using System.Text.Json:

```c#
public class SystemTextJsonSerializer : IRedisSerializer
{
    public RedisValue Serialize<T>(T instance)
    {
        return JsonSerializer.Serialize(instance);
    }

    public T Deserialize<T>(RedisValue value)
    {
        return JsonSerializer.Deserialize<T>(value);
    }
}

// Usage
var cache = new RedisCache(redis, "myapp")
{
    Serializer = new SystemTextJsonSerializer()
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

## PassThroughCache

`PassThroughCache` is a "null object" implementation of `ICache` that performs no caching. Every call to `GetSet` invokes the `calculateValue` callback, and `Invalidate` does nothing.

```c#
var cache = new PassThroughCache();
```

This is useful for:

- **Unit testing**: Inject `PassThroughCache` to test your code without actual caching behavior
- **Development**: Disable caching temporarily without changing your code structure
- **Feature flags**: Conditionally disable caching based on configuration

Example usage in tests:

```c#
[Fact]
public async Task GetProduct_ReturnsProductFromRepository()
{
    // Arrange - use PassThroughCache to bypass caching
    var cache = new PassThroughCache();
    var repository = new MockProductRepository();
    var service = new ProductService(cache, repository);

    // Act
    var product = await service.GetProductAsync(1);

    // Assert - verify the repository was called (not cached)
    Assert.Equal(1, repository.GetByIdCallCount);
}
```

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

## Thread Safety

All `ICache` implementations in this library are **thread-safe** and designed for concurrent access from multiple threads.

### Thundering Herd Prevention

When multiple threads simultaneously request the same uncached key, only **one** thread will execute the `calculateValue` callback. Other threads will wait and receive the same cached result. This prevents the "thundering herd" problem where expensive calculations are performed multiple times.

```c#
// Safe: Only one database call will be made, even if 100 requests arrive simultaneously
var value = await cache.GetSetAsync("expensive-key", async ct =>
{
    // This will only execute once, other concurrent callers wait
    return await ExpensiveDatabaseCall(ct);
}, TimeSpan.FromMinutes(5));
```

### Locking Behavior

- **`MemoryCache`** and **`RedisCache`** use per-key `ReaderWriterLockSlim` locks
- Read operations (cache hits) can proceed concurrently
- Write operations (cache misses, invalidations) are serialized per key
- Different keys do not block each other

### Exception Handling

If `calculateValue` throws an exception:

1. The exception propagates to the caller
2. The lock is properly released
3. No partial value is cached
4. Subsequent calls will retry the calculation

```c#
try
{
    var value = cache.GetSet("key", () =>
    {
        throw new Exception("Calculation failed");
    }, TimeSpan.FromMinutes(5));
}
catch (Exception)
{
    // Lock is released, cache remains functional
    // Next call will retry the calculation
}
```

### Singleton Usage

Cache instances should be registered as **singletons** in your DI container. Creating multiple instances of the same cache type will result in separate cache stores that don't share data.

```c#
// Correct: Single shared instance
services.AddSingleton<ICache>(new MemoryCache("myapp"));

// Incorrect: Each request gets a new empty cache
services.AddTransient<ICache>(sp => new MemoryCache("myapp")); // Don't do this!
```

## Error Handling

### Exceptions in calculateValue

When `calculateValue` throws an exception, the behavior is:

1. **Exception propagates**: The exception is re-thrown to the caller
2. **Nothing is cached**: Failed calculations don't store partial results
3. **Lock is released**: Other threads can proceed (and will retry)
4. **Cache remains functional**: The cache is not corrupted

```c#
// First call - throws exception
try
{
    var value = cache.GetSet("key", () =>
    {
        throw new DatabaseException("Connection failed");
    }, TimeSpan.FromMinutes(5));
}
catch (DatabaseException)
{
    // Handle error - nothing was cached
}

// Second call - can succeed if the issue is resolved
var value = cache.GetSet("key", () =>
{
    return database.GetValue(); // Works now
}, TimeSpan.FromMinutes(5));
// Value is now cached
```

### Transient Failures

For operations that may have transient failures (network issues, timeouts), consider implementing retry logic in your `calculateValue`:

```c#
var value = await cache.GetSetAsync("key", async ct =>
{
    // Retry up to 3 times with exponential backoff
    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            return await httpClient.GetStringAsync(url, ct);
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }
    throw new Exception("All retries failed");
}, TimeSpan.FromMinutes(5));
```

### Redis Connection Failures

When using `RedisCache`, if Redis is unavailable:

- `GetSet`/`GetSetAsync`: Throws `RedisConnectionException`
- `Invalidate`/`InvalidateAsync`: Throws `RedisConnectionException`

Consider wrapping with a fallback:

```c#
public async Task<T> GetWithFallbackAsync<T>(string key, Func<Task<T>> calculateValue, TimeSpan duration) where T : class
{
    try
    {
        return await _redisCache.GetSetAsync(key, _ => calculateValue(), duration);
    }
    catch (RedisConnectionException)
    {
        // Redis is down - fall back to direct calculation
        _logger.LogWarning("Redis unavailable, bypassing cache for key {Key}", key);
        return await calculateValue();
    }
}
```

### Serialization Errors

If serialization or deserialization fails (e.g., type mismatch, corrupted data):

- **Serialization failure**: Exception thrown, value not cached
- **Deserialization failure**: Exception thrown, treated as cache miss

When changing the structure of cached types, consider:

1. Using a new cache key prefix/version
2. Clearing the cache after deployment
3. Using backward-compatible serialization (e.g., protobuf with optional fields)