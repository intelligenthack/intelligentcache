using IntelligentHack.IntelligentCache;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace IntelligentCache.Tests
{
    public class DefaultRedisValueSerializerTests
    {
        public class ContentDto
        {
            public ContentDto(string value, DateTime creation)
            {
                Value = value;

                if (creation == default)
                {
                    throw new ArgumentException("Invalid creation date");
                }

                Creation = creation;
            }

            public string Value { get; }
            public DateTime Creation { get; }
        }

        [Fact]
        public void TryDeserialize_treates_ArgumentException_as_invalid_data()
        {
            // Arrange
            var sut = DefaultRedisValueSerializer.Instance;

            // Act
            var success = sut.TryDeserialize<ContentDto>("{ 'value': 'hello' }", out var _);

            // Assert
            Assert.False(success);
        }
    }
}
