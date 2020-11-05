using IntelligentHack.IntelligentCache;
using StackExchange.Redis;
using System;
using Xunit;

namespace IntelligentCache.Tests
{
    public class JsonStringSerializerTest
    {
        private class Model
        {
            public string? Foo { get; set; }
            public double Bar { get; set; }
        }

        [Fact]
        public void Deserialize_json_to_object()
        {
            // Arrange
            var foo = "foo";
            var bar = 13.5.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            var json = $"{{\"Foo\":\"{foo}\",\"Bar\":{bar}}}";
            var value = RedisValue.Unbox(json);
            IRedisSerializer serializer = new JsonStringSerializer();

            // Act
            var model = serializer.Deserialize<Model>(value);

            // Assert
            Assert.Equal(foo, model.Foo);
            Assert.Equal(13.5, model.Bar, 0);
        }

        [Fact]
        public void Serialize_object_to_json()
        {
            // Arrange
            var model = new Model() { Foo = "foo", Bar = 13.5 };
            var bar = model.Bar.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            var json = $"{{\"Foo\":\"{model.Foo}\",\"Bar\":{bar}}}";
            IRedisSerializer serializer = new JsonStringSerializer();

            // Act
            var redisValue = serializer.Serialize(model);

            //Assert
            Assert.Equal(json, redisValue.Box());
        }
    }
}
