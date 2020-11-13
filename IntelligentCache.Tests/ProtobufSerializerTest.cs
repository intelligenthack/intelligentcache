using IntelligentHack.IntelligentCache;
using ProtoBuf;
using System.ServiceModel.Channels;
using Xunit;

namespace IntelligentCache.Tests
{
    public class ProtobufSerializerTest
    {
        [ProtoContract]
        private class Model
        {
            [ProtoMember(1)]
            public string Foo { get; set; }

            [ProtoMember(2)]
            public double Bar { get; set; }
        }

        [Fact]
        public void Serialize_Deflate()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.Deflate };
            var redisValue = protobufSerializer.Serialize(model);
            Assert.True(redisValue.HasValue);
        }

        [Fact]
        public void Serialize_GZip()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.GZip };
            var redisValue = protobufSerializer.Serialize(model);
            Assert.True(redisValue.HasValue);
        }

        [Fact]
        public void Serialize_Default()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.None };
            var redisValue = protobufSerializer.Serialize(model);
            Assert.True(redisValue.HasValue);
        }

        [Fact]
        public void Deserialize_Deflate()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.Deflate };
            var deserialized = protobufSerializer.Deserialize<Model>(protobufSerializer.Serialize(model));
            Assert.Equal(model.Bar, deserialized.Bar);
            Assert.Equal(model.Foo, deserialized.Foo);
        }

        [Fact]
        public void Deserialize_GZip()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.GZip };
            var deserialized = protobufSerializer.Deserialize<Model>(protobufSerializer.Serialize(model));
            Assert.Equal(model.Bar, deserialized.Bar);
            Assert.Equal(model.Foo, deserialized.Foo);
        }

        [Fact]
        public void Deserialize_Default()
        {
            var model = new Model() { Bar = 15, Foo = "foo" };
            IRedisSerializer protobufSerializer = new ProtobufSerializer() { CompressionFormat = CompressionFormat.None };
            var deserialized = protobufSerializer.Deserialize<Model>(protobufSerializer.Serialize(model));
            Assert.Equal(model.Bar, deserialized.Bar);
            Assert.Equal(model.Foo, deserialized.Foo);
        }
    }
}
