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

var content = await cache.GetSet(cacheKey, async () =>
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
