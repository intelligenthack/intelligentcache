using IntelligentHack.IntelligentCache;
using Xunit;

namespace IntelligentCache.Tests
{
    public class RedisInvalidationReceiverTests
    {
        [Fact]
        public void Invalidation_messages_call_Invalidate_on_inner_cache()
        {
            // Arrange
            string? invalidatedKey = null;
            var innerCache = new InspectableCache(key => { invalidatedKey = key; });

            var subscriber = FakeRedis.CreateSubscriber();

            var sut = new RedisInvalidationReceiver(innerCache, subscriber, "invalidation");

            // Act
            subscriber.Publish("invalidation", "testKey");

            // Assert
            Assert.Equal("testKey", invalidatedKey);
        }
    }
}
