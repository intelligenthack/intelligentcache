using AutoFixture;
using StackExchange.Redis;
using Xunit;
using static IntelligentHack.IntelligentCache.RedisCache;

namespace IntelligentCache.Tests
{
    public class InvalidationMessageTests
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void Parse_roundtrips_without_losing_information()
        {
            var sut = _fixture.Create<InvalidationMessage>();

            RedisValue serializedValue = sut; // Encode
            InvalidationMessage deserializedValue = serializedValue; // Decode

            Assert.Equal(sut.ClientId, deserializedValue.ClientId);
            Assert.Equal(sut.Key, deserializedValue.Key);
        }
    }
}
