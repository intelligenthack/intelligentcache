#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
using IntelligentHack.IntelligentCache;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class MemoryCacheTests
    {
        private static int _nextCachePrefixId;

        private static string GeneratePrefix()
        {
            var prefixId = Interlocked.Increment(ref _nextCachePrefixId);
            return $"test{prefixId}";
        }

        [Fact]
        public void GetSet_when_key_missed_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal("42", result);
            Assert.True(called);
        }

        [Fact]
        public async Task GetSetAsync_when_key_missed_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var called = false;

            // Act
            var result = await sut.GetSetAsync("testKey", async ct => { called = true; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal("42", result);
            Assert.True(called);
        }

        [Fact]
        public void GetSet_when_key_hit_then_lambda_not_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.FromSeconds(20));
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("42", result);
            Assert.False(called);
        }

        [Fact]
        public async Task GetSetAsync_when_key_hit_then_lambda_not_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.FromSeconds(20));
            var called = false;

            // Act
            var result = await sut.GetSetAsync("testKey", async ct => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("42", result);
            Assert.False(called);
        }

        [Fact]
        public void GetSet_when_key_expired_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.Zero);
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("not 42", result);
            Assert.True(called);
        }

        [Fact]
        public async Task GetSetAsync_when_key_expired_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.Zero);
            var called = false;

            // Act
            var result = await sut.GetSetAsync("testKey", async ct => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("not 42", result);
            Assert.True(called);
        }

        [Fact]
        public void GetSet_when_key_invalidated_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "forty-two"; }, TimeSpan.FromSeconds(60));
            sut.Invalidate("testKey");
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(called);
            Assert.Equal("not 42", result);
        }

        [Fact]
        public async Task GetSetAsync_when_key_invalidated_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("testKey", () => { return "forty-two"; }, TimeSpan.FromSeconds(60));
            sut.Invalidate("testKey");
            var called = false;

            // Act
            var result = await sut.GetSetAsync("testKey", async ct => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(called);
            Assert.Equal("not 42", result);
        }

        [Fact]
        public void GetSet_allows_infinite_expiration()
        {
            // This test checks for a bug that was present when using an infinite expiration.

            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "42"; }, TimeSpan.MaxValue);

            // Assert
            Assert.Equal("42", result);
            Assert.True(called);
        }

        [Fact]
        public void GetSet_returns_null_when_trying_to_save_null_values()
        {
            // Arrange
            var sut = new MemoryCache("MemoryCache-InvalidOperationException ");
            var called = false;
            var cachedValue = sut.GetSet<string>("testKey", () => { called = true; return null; }, TimeSpan.Zero);

            // Act-Assert
            Assert.Null(cachedValue);
            Assert.True(called);
        }
    }
}
