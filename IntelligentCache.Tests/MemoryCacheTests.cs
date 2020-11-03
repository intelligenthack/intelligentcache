using IntelligentHack.IntelligentCache;
using System;
using Xunit;

namespace IntelligentCache.Tests
{
    public class MemoryCacheTests
    {
        [Fact]
        public void GetSet_when_key_missed_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache("test1");
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal("42", result);
            Assert.True(called);
        }

        [Fact]
        public void GetSet_when_key_hit_then_lambda_not_called()
        {
            // Arrange
            var sut = new MemoryCache("test2");
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.FromSeconds(20));
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("42", result);
            Assert.False(called);
        }

        [Fact]
        public void GetSet_when_key_expired_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache("test3");
            sut.GetSet("testKey", () => { return "42"; }, TimeSpan.Zero);
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal("not 42", result);
            Assert.True(called);
        }

        [Fact]
        public void GetSet_when_key_invalidated_then_lambda_called()
        {
            // Arrange
            var sut = new MemoryCache("test4");
            sut.GetSet("testKey", () => { return "forty-two"; }, TimeSpan.FromSeconds(60));
            sut.Invalidate("testKey");
            var called = false;

            // Act
            var result = sut.GetSet("testKey", () => { called = true; return "not 42"; }, TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(called);
            Assert.Equal("not 42", result);
        }

    }
}
