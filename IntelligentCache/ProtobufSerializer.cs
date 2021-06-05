using ProtoBuf;
using StackExchange.Redis;
using System;
using System.IO;
using System.IO.Compression;
using System.ServiceModel.Channels;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="IRedisSerializer" /> that encodes objects as compressed protobuf.
    /// </summary>
    public class ProtobufSerializer : IRedisSerializer
    {
        public CompressionFormat CompressionFormat { get; set; } = CompressionFormat.GZip;

        public T Deserialize<T>(RedisValue value)
        {
            switch (CompressionFormat)
            {
                case CompressionFormat.Deflate:
                    using (var stream = new DeflateStream(new MemoryStream(value), CompressionMode.Decompress))
                    {
                        return Serializer.Deserialize<T>(stream);
                    }
                case CompressionFormat.GZip:
                    using (var stream = new GZipStream(new MemoryStream(value), CompressionMode.Decompress))
                    {
                        return Serializer.Deserialize<T>(stream);
                    }
                case CompressionFormat.None:
                    return Serializer.Deserialize<T>(value);
                default:
                    throw new InvalidOperationException("Unknown CompressionFormat");
            }
        }

        public RedisValue Serialize<T>(T instance)
        {
            switch (CompressionFormat)
            {
                case CompressionFormat.Deflate:
                    using (var memStream = new MemoryStream())
                    {
                        using (var stream = new DeflateStream(memStream, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            Serializer.Serialize(stream, instance);
                        }
                        return RedisValue.CreateFrom(memStream);
                    }
                case CompressionFormat.GZip:
                    using (var memStream = new MemoryStream())
                    {
                        using (var stream = new GZipStream(memStream, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            Serializer.Serialize(stream, instance);
                        }
                        return RedisValue.CreateFrom(memStream);
                    }
                case CompressionFormat.None:
                    using (var stream = new MemoryStream())
                    {
                        Serializer.Serialize(stream, instance);
                        return RedisValue.CreateFrom(stream);
                    }
               default:
                    throw new InvalidOperationException("Unknown CompressionFormat");
            }
        }
    }
}
