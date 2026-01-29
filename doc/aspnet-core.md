# ASP.NET Core Integration

This guide explains how to integrate IntelligentCache with ASP.NET Core's dependency injection system.

## Basic Setup

### Memory Cache Only

For applications that only need local in-memory caching:

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<ICache>(sp => new MemoryCache("myapp"));
```

### Redis Cache Only

For distributed caching with Redis:

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddSingleton<ICache>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    return new RedisCache(redis, "myapp");
});
```

### Two-Level Cache (Memory + Redis)

For optimal performance with a local cache backed by Redis:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddSingleton<ICache>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    return new CompositeCache(
        new MemoryCache("myapp-l1"),
        new RedisCache(redis, "myapp-l2")
    );
});
```

### Web Farm with Invalidation Propagation

For web farms where cache invalidation must propagate to all servers:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddSingleton<ICache>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var subscriber = redis.GetSubscriber();
    var invalidationChannel = RedisChannel.Literal("myapp:cache-invalidations");

    return new CompositeCache(
        new RedisInvalidationReceiver(
            new MemoryCache("myapp-l1"),
            subscriber,
            invalidationChannel
        ),
        new CompositeCache(
            new RedisCache(redis, "myapp-l2"),
            new RedisInvalidationSender(subscriber, invalidationChannel)
        )
    );
});
```

## Using the Cache

Inject `ICache` into your controllers or services:

```csharp
public class ProductService
{
    private readonly ICache _cache;
    private readonly IProductRepository _repository;

    public ProductService(ICache cache, IProductRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Product> GetProductAsync(int productId, CancellationToken ct = default)
    {
        return await _cache.GetSetAsync(
            $"product:{productId}",
            async token => await _repository.GetByIdAsync(productId, token),
            TimeSpan.FromMinutes(10),
            ct
        );
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
    {
        await _repository.UpdateAsync(product, ct);
        await _cache.InvalidateAsync($"product:{product.Id}", ct);
    }
}
```

## Testing

For unit tests, use `PassThroughCache` which doesn't cache anything:

```csharp
public class ProductServiceTests
{
    [Fact]
    public async Task GetProductAsync_ReturnsProduct()
    {
        // Arrange
        var cache = new PassThroughCache(); // No actual caching
        var repository = new MockProductRepository();
        var service = new ProductService(cache, repository);

        // Act
        var product = await service.GetProductAsync(1);

        // Assert
        Assert.NotNull(product);
    }
}
```

## Configuration from appsettings.json

You can load Redis connection strings from configuration:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false",
    "CachePrefix": "myapp"
  }
}
```

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Redis:ConnectionString"];
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddSingleton<ICache>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var prefix = config["Redis:CachePrefix"] ?? "cache";
    return new RedisCache(redis, prefix);
});
```

## Health Checks

You can add a health check for Redis connectivity:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("redis", () =>
    {
        try
        {
            var redis = builder.Services.BuildServiceProvider()
                .GetRequiredService<IConnectionMultiplexer>();
            return redis.IsConnected
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Redis is not connected");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    });
```

## Scoped vs Singleton

`ICache` implementations are designed to be registered as **Singleton**:

- `MemoryCache` uses a shared `System.Runtime.Caching.MemoryCache` instance
- `RedisCache` uses a shared `IConnectionMultiplexer` (which should also be singleton)
- The internal locking mechanisms are designed for concurrent access

Do **not** register as Scoped or Transient, as this would create multiple cache instances that don't share state.
