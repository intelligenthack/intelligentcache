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
