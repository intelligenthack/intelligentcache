<img src="doc/logo.png?raw=true" width="200">

# Intelligent Cache

This package implements a distributed cache monad ("pattern") and currently supports single and multiple layers of caching, in memory and via Redis.

To use the pattern, you will interact with an object of type ICache, where you can use the following operations:

```

// Get something from the cache, on cache fail the Func is called to refresh the value and stored
var foo = myCache.GetSet("foo-cache-key", ()=>{ return foo-from-db(); }, Timespan.FromHours(1) );

// alternatively

// Invalidate the cache, in case we've modified foo in the db so the cache is stale
myCache.Invalidate();

```

The `ICache` object can be of different kinds -- we currently offer a memory cache for local caching and a Redis cache for distributed caching. What makes the pattern a monad is that different caches can be *composed* and this allows seamless multilayer caching.

For example, to implement a multilayer cache with a local layer and a Redis layer:

```
var memoryCache = new MemoryCache();
var redisCache = new RedisCache(/* params */);
var cache = new CompositeCache(memoryCache, redisCache);
```

Note that this cache does not invalidate correctly in a webfarm environment: invalidations will work on the local server and redis but not the other webfarm webservers. In order to propagate invalidation we introduced two new composable ICache objects: `RedisInvalidatorSender` and `RedisInvalidatorReceiver`.

In order to create a local cache that invalidates when the remote cache is nuked, you can follow this composition pattern:

```
new CompositeCache(
    new RedisInvalidatorReceiver(new MemoryCache()),
    new CompositeCache(
        new RedisInvalidatorSender(),
        new RedisCache())
)
```
# Details

- [API Documentation](doc/api-documentation.md)  

- [Using with Asp.Net-Core](doc/dotnet-core.md)  

- [Architecture](doc/architecture.md)

- [Requirements and best practices](doc/best-practices.md)

# Upgrading from a previous version

This package follows [semantic versioning](https://semver.org/), which means that upgrading to a higher MINOR or PATCH version should always work. Upgrading to a higher MAJOR version will require code changes. Make sure to read the release notes before upgrading.

# Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

# License

This code is released under the MIT license. Please refer to [LICENSE.md](LICENSE.md) for details.
