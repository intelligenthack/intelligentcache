# Distributed Cache

This package implements a distributed cache backed by Redis.
The cache has two levels. The first one is in memory and the second one is Redis.
When looking up a key, the memory cache is checked first. If the value is not found, redis is checked. The underlying data source is only consulted if the key is not found in any cache level.

## Configuration

Register the required services:

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddRedisDistributedCache(Configuration.GetConnectionString("Redis"), ex => ex.LogNoContext());
}
```

## Usage

### Reading a value from the cache

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

### Invalidating a value from the cache

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
