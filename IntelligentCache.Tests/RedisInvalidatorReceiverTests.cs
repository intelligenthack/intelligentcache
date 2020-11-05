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

            var topic = new FakeRedisTopic();
            var sut = new RedisInvalidatorReceiver(topic.Subscriber, innerCache, "invalidation");

            // Act
            topic.Publish("invalidation", "testKey");

            // Assert
            Assert.Equal("testKey", invalidatedKey);
        }
    }
}
