using IntelligentHack.IntelligentCache;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class InputValidationTests
    {
        [Fact]
        public void MemoryCache_constructor_with_null_prefix_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryCache(null));
        }

        [Fact]
        public void MemoryCache_constructor_with_empty_prefix_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryCache(""));
        }

        [Fact]
        public void RedisCache_constructor_with_null_multiplexer_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RedisCache(null, "prefix"));
        }

        [Fact]
        public void RedisCache_constructor_with_null_prefix_throws()
        {
            var multiplexer = FakeRedis.CreateConnectionMultiplexer();
            Assert.Throws<ArgumentNullException>(() => new RedisCache(multiplexer, null));
        }

        [Fact]
        public void RedisCache_constructor_with_empty_prefix_throws()
        {
            var multiplexer = FakeRedis.CreateConnectionMultiplexer();
            Assert.Throws<ArgumentNullException>(() => new RedisCache(multiplexer, ""));
        }

        [Fact]
        public void CompositeCache_constructor_with_null_level1_throws()
        {
            var cache = new MemoryCache("test");
            Assert.Throws<ArgumentNullException>(() => new CompositeCache(null, cache));
        }

        [Fact]
        public void CompositeCache_constructor_with_null_level2_throws()
        {
            var cache = new MemoryCache("test");
            Assert.Throws<ArgumentNullException>(() => new CompositeCache(cache, null));
        }

        [Fact]
        public void RedisInvalidationSender_constructor_with_null_subscriber_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RedisInvalidationSender(null, RedisChannel.Literal("channel")));
        }

        [Fact]
        public void RedisInvalidationSender_constructor_with_empty_channel_throws()
        {
            var subscriber = FakeRedis.CreateSubscriber();
            Assert.Throws<ArgumentException>(() =>
                new RedisInvalidationSender(subscriber, default));
        }

        [Fact]
        public void RedisInvalidationReceiver_constructor_with_null_inner_throws()
        {
            var subscriber = FakeRedis.CreateSubscriber();
            Assert.Throws<ArgumentNullException>(() =>
                new RedisInvalidationReceiver(null, subscriber, RedisChannel.Literal("channel")));
        }

        [Fact]
        public void RedisInvalidationReceiver_constructor_with_null_subscriber_throws()
        {
            var cache = new MemoryCache("test");
            Assert.Throws<ArgumentNullException>(() =>
                new RedisInvalidationReceiver(cache, null, RedisChannel.Literal("channel")));
        }

        [Fact]
        public void GetSet_with_null_key_stores_with_null_key()
        {
            // Note: The current implementation does not validate null keys.
            // This test documents the current behavior.
            var cache = new MemoryCache("test-null-key");

            // Act - this should work (keys get prefixed, so "prefix:null" becomes the key)
            var result = cache.GetSet<string>(null, () => "value", TimeSpan.FromMinutes(1));

            // Assert
            Assert.Equal("value", result);
        }

        [Fact]
        public void GetSet_with_empty_key_stores_with_empty_key()
        {
            // Note: The current implementation does not validate empty keys.
            // This test documents the current behavior.
            var cache = new MemoryCache("test-empty-key");

            // Act
            var result = cache.GetSet("", () => "value", TimeSpan.FromMinutes(1));

            // Assert
            Assert.Equal("value", result);
        }

        [Fact]
        public void GetSet_with_special_characters_in_key_works()
        {
            var cache = new MemoryCache("test-special");
            var specialKey = "key:with:colons/and/slashes?and=query&params#hash";

            // Act
            var result = cache.GetSet(specialKey, () => "value", TimeSpan.FromMinutes(1));

            // Assert
            Assert.Equal("value", result);

            // Verify it's cached
            var called = false;
            var result2 = cache.GetSet(specialKey, () => { called = true; return "other"; }, TimeSpan.FromMinutes(1));
            Assert.False(called);
            Assert.Equal("value", result2);
        }

        [Fact]
        public async Task GetSetAsync_with_null_calculateValue_throws()
        {
            var cache = new MemoryCache("test-null-calc");

            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await cache.GetSetAsync<string>("key", null, TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public void GetSet_with_null_calculateValue_throws()
        {
            var cache = new MemoryCache("test-null-calc-sync");

            Assert.Throws<NullReferenceException>(() =>
                cache.GetSet<string>("key", null, TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public void GetSet_with_negative_duration_works()
        {
            // Negative duration means immediate expiration
            var cache = new MemoryCache("test-negative");

            var callCount = 0;
            cache.GetSet("key", () => { callCount++; return "value1"; }, TimeSpan.FromSeconds(-1));
            cache.GetSet("key", () => { callCount++; return "value2"; }, TimeSpan.FromSeconds(-1));

            // Both calls should calculate because duration is negative (immediate expiry)
            Assert.Equal(2, callCount);
        }
    }
}
