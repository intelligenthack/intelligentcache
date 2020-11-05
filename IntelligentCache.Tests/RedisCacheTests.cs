#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

using IntelligentHack.IntelligentCache;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class RedisCacheTests
    {
        [Fact]
        public void GetSet_calls_calculateValue_on_miss()
        {
            // Arrange
            string? setKey = null;
            string? setValue = null;
            TimeSpan? setExpiration = default;

            var multiplexer = FakeRedis.Create(onSet: (key, value, expiration) =>
            {
                setKey = key;
                setValue = value;
                setExpiration = expiration;
            });

            var sut = new RedisCache(multiplexer, "prefix");
            var called = false;

            // Act
            var valueFromCache = sut.GetSet("testKey", () => { called = true; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(called);
            Assert.Equal("42", valueFromCache);
            Assert.Equal("prefix:testKey", setKey);
            Assert.Equal("\"42\"", setValue); // Json-encoded
            Assert.Equal(TimeSpan.FromSeconds(10), setExpiration);
        }

        [Fact]
        public async Task GetSetAsync_calls_calculateValue_on_miss()
        {
            // Arrange
            string? setKey = null;
            string? setValue = null;
            TimeSpan? setExpiration = default;

            var multiplexer = FakeRedis.Create(onSet: (key, value, expiration) =>
            {
                setKey = key;
                setValue = value;
                setExpiration = expiration;
            });

            var sut = new RedisCache(multiplexer, "prefix");
            var called = false;

            // Act
            var valueFromCache = await sut.GetSetAsync("testKey", async ct => { called = true; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(called);
            Assert.Equal("42", valueFromCache);
            Assert.Equal("prefix:testKey", setKey);
            Assert.Equal("\"42\"", setValue); // Json-encoded
            Assert.Equal(TimeSpan.FromSeconds(10), setExpiration);
        }

        [Fact]
        public void GetSet_uses_cached_value_on_hit()
        {
            // Arrange
            string? lookupKey = null;

            var multiplexer = FakeRedis.Create(onGet: key => { lookupKey = key; return "\"42\""; });

            var sut = new RedisCache(multiplexer, "prefix");
            var called = false;

            // Act
            var valueFromCache = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.False(called);
            Assert.Equal("42", valueFromCache);
            Assert.Equal("prefix:testKey", lookupKey);
        }

        [Fact]
        public async Task GetSetAsync_uses_cached_value_on_hit()
        {
            // Arrange
            string? lookupKey = null;

            var multiplexer = FakeRedis.Create(onGet: key => { lookupKey = key; return "\"42\""; });

            var sut = new RedisCache(multiplexer, "prefix");
            var called = false;

            // Act
            var valueFromCache = await sut.GetSetAsync("testKey", async ct => { called = true; return "not 42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.False(called);
            Assert.Equal("42", valueFromCache);
            Assert.Equal("prefix:testKey", lookupKey);
        }

        [Fact]
        public void Invalidate_clears_the_value()
        {
            // Arrange
            string? setKey = null;
            RedisValue setValue = default;
            TimeSpan? setExpiration = default;

            var multiplexer = FakeRedis.Create(onSet: (key, value, expiration) =>
            {
                setKey = key;
                setValue = value;
                setExpiration = expiration;
            });

            var sut = new RedisCache(multiplexer, "prefix");

            // Act
            sut.Invalidate("testKey");

            // Assert
            Assert.Equal("prefix:testKey", setKey);
            Assert.True(setValue.IsNull);
            Assert.Null(setExpiration);
        }

        [Fact]
        public async Task InvalidateAsync_clears_the_value()
        {
            // Arrange
            string? setKey = null;
            RedisValue setValue = default;
            TimeSpan? setExpiration = default;

            var multiplexer = FakeRedis.Create(onSet: (key, value, expiration) =>
            {
                setKey = key;
                setValue = value;
                setExpiration = expiration;
            });

            var sut = new RedisCache(multiplexer, "prefix");

            // Act
            await sut.InvalidateAsync("testKey");

            // Assert
            Assert.Equal("prefix:testKey", setKey);
            Assert.True(setValue.IsNull);
            Assert.Null(setExpiration);
        }
    }
}
