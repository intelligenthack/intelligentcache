using IntelligentHack.IntelligentCache;
using Xunit;

namespace IntelligentCache.Tests
{
    public class RedisInvalidatorReceiverTests
    {
        [Fact]
        public void Invalidation_messages_call_Invalidate_on_inner_cache()
        {
            // Arrange
            string? invalidatedKey = null;
            var innerCache = new InspectableCache(key => { invalidatedKey = key; });

            var subscriber = FakeRedis.CreateSubscriber();

            var sut = new RedisInvalidatorReceiver(subscriber, innerCache, "invalidation");

            // Act
            subscriber.Publish("invalidation", "testKey");

            // Assert
            Assert.Equal("testKey", invalidatedKey);
        }
    }
}
