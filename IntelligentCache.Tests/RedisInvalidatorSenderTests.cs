#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

using IntelligentHack.IntelligentCache;
using System;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class RedisInvalidatorSenderTests
    {
        [Fact]
        public void Invalidate_publishes_an_invalidation_message()
        {
            // Arrange
            string? publishedChannel = null;
            string? publishedMessage = null;

            var subscriber = FakeRedis.CreateSubscriber(onPublish: (c, m) =>
            {
                publishedChannel = c;
                publishedMessage = m;
            });

            var sut = new RedisInvalidatorSender(subscriber, "invalidation");

            // Act
            sut.Invalidate("testKey");

            // Assert
            Assert.Equal("invalidation", publishedChannel);
            Assert.Equal("testKey", publishedMessage);
        }

        [Fact]
        public async Task InvalidateAsync_publishes_an_invalidation_message()
        {
            // Arrange
            string? publishedChannel = null;
            string? publishedMessage = null;

            var subscriber = FakeRedis.CreateSubscriber(onPublish: (c, m) =>
            {
                publishedChannel = c;
                publishedMessage = m;
            });

            var sut = new RedisInvalidatorSender(subscriber, "invalidation");

            // Act
            await sut.InvalidateAsync("testKey");

            // Assert
            Assert.Equal("invalidation", publishedChannel);
            Assert.Equal("testKey", publishedMessage);

        }

        [Fact]
        public void GetSet_always_calculates_the_value()
        {
            // Arrange
            var subscriber = FakeRedis.CreateSubscriber();
            var sut = new RedisInvalidatorSender(subscriber, "invalidation");

            var count = 0;

            // Act
            var valueFromCache = sut.GetSet("testKey", () => { ++count; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(1, count);
            Assert.Equal("42", valueFromCache);
        }

        [Fact]
        public async Task GetSetAsync_always_calculates_the_value()
        {
            // Arrange
            var subscriber = FakeRedis.CreateSubscriber();
            var sut = new RedisInvalidatorSender(subscriber, "invalidation");

            var count = 0;

            // Act
            var valueFromCache = await sut.GetSetAsync("testKey", async ct => { ++count; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(1, count);
            Assert.Equal("42", valueFromCache);
        }
    }
}
