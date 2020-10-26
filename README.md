![intelligent cache](doc/logo.png?raw=true)

# Intelligent Cache

This package implements a distributed cache monad ("pattern") and currently supports single and multiple layers of caching, in memory and via Redis.

To use the pattern, you will interact with an object of type ICache, where you can use the following operations for the async case:

```c#
ICache myCache = get-cache-singleton();

// Get something from the cache, on cache fail the Func is called to refresh the value and stored
var foo = myCache.GetSetAsync("foo-cache-key", ()=>{ return foo-from-db(); }, 3660 /* seconds */);

// Invalidate the cache, in case we've modified foo in the db so the cache is stale
myCache.InvalidateAsync("foo-cache-key");

```

The `ICache` object can be of different kinds -- we currently offer a memory cache for local caching and a Redis cache for distributed caching. What makes the pattern a monad is that different caches can be *composed* and this allows seamless multilayer caching.

For example, to implement a multilayer cache with a local layer and a Redis layer:

```c#
var redis = new RedisConnection(/* connection string */);
redis.StartAsync(/* cancellation token */);

var memoryCache = new MemoryCache();
var redisCache = new RedisCache(redis);
var cache = new CompositeCache(memoryCache, redisCache);
```

Note that `RedisConnection` class implements `IHostedService`, since it needs to start a background task. That is why `StartAsync` should be called after instantiating the cache. Another way of doing so is using `AddHostedService` as shown in the "Using with Asp.Net-Core" section. 

With the above configuration, when a key is invalidated in one process, other processes may still have that key in their memory cache. The `RedisInvalidationPropagator` class can be used to propagate the invalidations across processes as follows:

```c#
var redis = new RedisConnection(/* connection string */);
redis.StartAsync(/* cancellation token */);

var memoryCache = new MemoryCache();
var redisCache = new RedisCache(redis);

var cache = new RedisInvalidationPropagator(
    new CompositeCache(memoryCache, redisCache),
    redis
);
```

That's all you need. All operations are already correctly wired to implement the two layers. Clearly you can add more layers and types as you need.

Alternatively you can call the already provided extension method `AddRedisIntelligentCache`.
```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisIntelligentCache(new RedisCache("localhost:6379"));
}
```
Even customize `RedisCache`:
```c#
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

## Usage

### Reading a value from the cache

There is a single operation to read from the cache and compute the value in case of a miss, called `GetSetAsync` or `GetSet` for the synchronous version. It takes a cache key, a callback and a duration. If the cache contains a value for the specified key, that value is returned immediately. Otherwise, the callback is invoked to compute the value, which is then stored into the cache for the specified duration.

In most cases, caching data is as simple as wrapping the existing code by a call to `GetSet` or `GetSetAsync`.

The following example shows how to get a value from the cache.

```c#
ICache cache = ...; // Get the cache from the DI container
string contentId = ...;
string cacheKey = "<some unique constructed key value, usually derived from contentId>";

var cachedValue = await cache.GetSetAsync(cacheKey, async () =>
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

The `GetSet` and the corresponding async methods have multiple overloads to allow different representations of the same parameters, which can be summarized as follows.

| Name | Description
|-|-|
| `key` | The key used to lookup the value. This must uniquely identify the content and, in general, should be derived from the identifier of the value. |
| `calculateValue` | A callback that is invoked in case of a cache miss to calculate the value. |
| `duration` / `durationInSeconds` | How long the value should be kept in the cache. If this parameter is omitted, the item never expires. |
| `cancellationToken` | An optional `CancellationToken` to cancel the asynchronous operations, only present in `GetSetAsync`. |

### Invalidating a value from the cache

When the source of cached data is modified, it may be desirable to invalidate the corresponding cache entry so that updated content is returned the next time it is requested. This is performed by calling the `Invalidate` method, or the async version, as shown in the following example.

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
await cache.InvalidateAsync(cacheKey);
```

The `InvalidateAsync` method takes the following parameters:

| Name | Description
|-|-|
| `key` | The key that was used previously to lookup the value. |

## Using with Asp.Net-Core

Register the cache as a services in your service collection by using the following code:

```c#
var redis = new RedisConnection(redisConnectionString);

var cache = new RedisInvalidationPropagator(
    new CompositeCache(
        level1: new MemoryCache(),
        level2: new RedisCache(redis)
    ),
    redis
);

services
    .AddSingleton<ICache>(sp => cache)
    .AddHostedService(sp => redis);
```

Alternatively you can call the already provided extension method `AddRedisIntelligentCache`.
```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisIntelligentCache("localhost:6379");
}
```
Even customize `RedisCache`:
```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisIntelligentCache("localhost:6379", options =>
    {
        options.ExceptionLogger = ex => Console.WriteLine(ex);
        options.KeyPrefix = "mychache:";
        options.ValueSerializer = new MyRedisValueSerializerImplementation();
    });
}
```

### Customizing Redis cache

It is possible to customize the serialization, exception logs and key prefix appended to all keys that are stored on Redis.

#### Custom serialization
Serialization can be customized by setting an implementation of `IRedisValueSerializer` to the `ValueSerializer` property on `RedisCache` class.

#### Custom exception logs
Exception logs can be changed by providing a new action for `ExceptionLogger` property. By default all exception will be logged to stderr.

#### Custom key prefix
A new key prefix of your choice can be applied by assigning it to `KeyPrefix` property .

# Architecture

A cache is a component that stores previously computed values for a period of time, so that the next time the value is needed, the computation can be avoided. The premise is that it is cheaper to look-up a value from the cache than computing that value.

This library implements a cache with multiple hierarchical levels. It means that whenever a lookup is performed, the first level is checked. If a value is found (hit), that value is returned and the second level is not contacted. If the value is not found in the first level (miss), the second level is checked ans so on, recursively.

![A sequence diagram showing three interactions between the application and two cache levels. In the first one, the application requests a key which is found in the first level and returned immediately. In that case the second level isn't contacted at all. In the second interaction, the application requests a key which is not found on the first level, but is found on the second level. The value is stored in the first level and returned to the application. The last interaction shows the application requesting a key that is not found in either cache levels. A new value is then computed, which is stored into both levels.](doc/levels.drawio.svg)

Although the architecture supports an arbitrary number of levels, the predefined configuration is to have an in-process, in-memory cache as the first level, and a shared redis instance as the second level.

## Cache invalidation

The library implements a cache invalidation mechanism that can be used to force the cache to compute the value the next time it is requested. This is useful when the underlying data is modified and we want to make sure that the changes are immediately visible.
Cache invalidation requires synchronization between all the instances of the application, to ensure that their local memory cache is invalidated as well. This synchronization is implemented through Redis' pub/sub mechanism, by broadcasting an invalidation message to all instances.

## Consistency considerations

The cache makes no consistency guarantees. Two instances of the application might contain different values for a given key. The Redis level reduces the likelihood of inconsistencies since it serves as a shared source for all instances of the application.

There are no consistency guarantees between different instances of the application. It is possible for two instances of the application to have different versions of the same data in cache, as illustrated in the following diagram.

![A temporal diagram that shows how different instances of the application can have an inconsistent view of the same data. Instance A requests a key which is found in the Redis level and stored in the in-memory level. Later, that value expires from Redis. When another instance of the application requests the same key, a new value is computed and stored in that instance's memory level. Until the old value expires from the first application instance's memory level, that instance still sees the old value.](doc/consistency.drawio.svg)

# Requirements and best practices

## Values should be immutable

Once a value is cached, it should not be modified, since other instances of the application will not see the change. Either make the type immutable or be careful of not modifying it.

## Value serialization

In order to be able to store values on Redis, they need to be serialized. By default, values are serialized using [Json.NET](https://www.newtonsoft.com/json). Therefore you must make sure that all the values that are stored into the cache can be serialized in that way.

As mentioned before, it is possible to customize the serialization by setting an implementation of `IRedisValueSerializer` to the `ValueSerializer` property on `RedisCache` class.

Since values can be stored in Redis for a long period of time, it is important to be careful when changing type of the values that are cached. Any property that is added, modified or renamed may cause incomplete data to be retrieved. For example, consider the following code that retrieves content from a database and uses the cache to avoid hitting the database on every request:

```c#
public class Content
{
    public string Value { get; set; }
}
```

```c#
ICache cache = ...; // Get the cache from the DI container
string contentId = ...;
string cacheKey = $"content:{contentId}";

var content = await cache.GetSetAsync(cacheKey, async () =>
{
    // Read the value from the DB.
    // This callback will only be executed if the value was not found in the cache.
    using var sqlConnection = OpenConnection();
    return await sqlConnection.QueryFirstOrDefaultAsync<Content>(
        "select Value from Content where Id = @contentId",
        new { contentId }
    );
}, 10); // Keep in cache for 10 seconds

// Render a page containing the value of the content
```

Suppose that we later decide to add a timestamp to the content, so that we can show when the content was created. We could add a property to the `Content` class as follows:

```c#
public class Content
{
    public string Value { get; set; }
    public DateTime Creation { get; set; }
}
```

After making this change, any content that was previously cached on Redis will be incomplete, because it won't have a creation date. There are different ways to solve this problem. The simplest is to clear the Redis cache. The following command will delete everything with the `cache:` prefix:

```bash
redis-cli --scan --pattern "cache:*" | xargs redis-cli del
```

If clearing the cache is not desirable, there are other options. In some cases it is possible to assign a default value to the newly added property. Another option is to use a constructor to perform any needed validation. If the constructor throws an `ArgumentException`, it will be treated as a cache miss. The following code shows a possible implementation:

```c#
public class Content
{
    public Content(string Value, DateTime Creation)
    {
        Value = value;
        Creation = creation != default
            ? creation
            : throw new ArgumentException("Invalid creation date");
    }

    public string Value { get; }
    public DateTime Creation { get; }
}
```

# Upgrading from a previous version

This package follows [semantic versioning](https://semver.org/), which means that upgrading to a higher MINOR or PATCH version should always work. Upgrading to a higher MAJOR version will require code changes. Make sure to read the release notes before upgrading.

# Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

# License

This code is released under the MIT license. Please refer to [LICENSE.md](LICENSE.md) for details.
