<img src="doc/logo.png?raw=true" width="200">

# Intelligent Cache

This package implements a distributed cache monad ("pattern") and currently supports single and multiple layers of caching, in memory and via Redis.

To use the pattern, you will interact with an object of type ICache, where you can use the following operations:

```c#
// Get something from the cache, on cache miss the Func is called to refresh the value and stored
var foo = myCache.GetSet("foo-cache-key", ()=>{ return fooFromDb(); }, Timespan.FromHours(1) );

// alternatively

// Invalidate the cache, in case we've modified foo in the db so the cache is stale
myCache.Invalidate("foo-cache-key");
```

The `ICache` object can be of different kinds -- we currently offer a memory cache for local caching and a Redis cache for distributed caching. What makes the pattern a monad is that different caches can be *composed* and this allows seamless multilayer caching.

For example, to implement a multilayer cache with a local layer and a Redis layer:

```c#
var memoryCache = new MemoryCache();
var redisCache = new RedisCache(/* params */);
var cache = new CompositeCache(memoryCache, redisCache);
```

Note that this cache does not invalidate correctly in a webfarm environment: invalidations will work on the local server and redis but not the other webfarm webservers. In order to propagate invalidation we introduced two new composable ICache objects: `RedisInvalidatorSender` and `RedisInvalidatorReceiver`.

In order to create a local cache that invalidates when the remote cache is nuked, you can follow this composition pattern:

```c#
new CompositeCache(
    new RedisInvalidatorReceiver(new MemoryCache()),
    new CompositeCache(
        new RedisCache(),
        new RedisInvalidatorSender())
)
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

The `GetSet` and the corresponding async methods have multiple overloads to allow different representations of the same parameters, which can be summarized as follows.

| Name | Description
|-|-|
| `key` | The key used to lookup the value. This must uniquely identify the content and, in general, should be derived from the identifier of the value. |
| `calculateValue` | A callback that is invoked in case of a cache miss to calculate the value. |
| `duration` | How long the value should be kept in the cache. If this parameter is omitted, the item never expires. |
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
await cache.Invalidate(cacheKey);
```

The `Invalidate` method takes the following parameters:

| Name | Description
|-|-|
| `key` | The key that was used previously to lookup the value. |

### Customizing Redis cache

It is possible to customize the serialization, exception logs and key prefix appended to all keys that are stored on Redis.

#### Custom serialization
Serialization can be customized by setting an implementation of `IRedisValueSerializer` to the `ValueSerializer` property on `RedisCache` class.

#### Custom key prefix
A new key prefix of your choice can be specified in the `RedisCache` constructor.

# Requirements and best practices

## Values should be immutable

Once a value is cached, it should not be modified, since other instances of the application will not see the change. Either make the type immutable or be careful of not modifying it.

## Value serialization

In order to be able to store values on Redis, they need to be serialized. By default, values are serialized using [Json.NET](https://www.newtonsoft.com/json). Therefore you must make sure that all the values that are stored into the cache can be serialized in that way.

As mentioned before, it is possible to customize the serialization by setting an implementation of `IRedisValueSerializer` to the `ValueSerializer` property on `RedisCache` class.

Since values can be stored in Redis for a long period of time, it is important to be careful when changing type of the values that are cached. Any property that is added, modified or renamed may cause incomplete data to be retrieved. After making such change, any content that was previously cached on Redis will be incomplete. The simplest way to solve this problem is to clear the Redis cache. The following command will delete everything with the `cache:` prefix:

```bash
redis-cli --scan --pattern "cache:*" | xargs redis-cli del
```

# Upgrading from a previous version

This package follows [semantic versioning](https://semver.org/), which means that upgrading to a higher MINOR or PATCH version should always work. Upgrading to a higher MAJOR version will require code changes. Make sure to read the release notes before upgrading.

# Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

# License

This code is released under the MIT license. Please refer to [LICENSE.md](LICENSE.md) for details.
