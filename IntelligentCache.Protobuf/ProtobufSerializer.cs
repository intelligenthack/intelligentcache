using System.IO.Compression;
using System.ServiceModel.Channels;
using Microsoft.IO;
using ProtoBuf;
using StackExchange.Redis;

namespace IntelligentHack.IntelligentCache.Protobuf
{
    /// <summary>
    /// An implementation of <see cref="IRedisSerializer" /> that encodes objects as compressed protobuf.
    /// </summary>
    public class ProtobufSerializer : IRedisSerializer
    {
        private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();
        
        public CompressionFormat CompressionFormat { get; set; } = CompressionFormat.GZip;

        public T? Deserialize<T>(RedisValue value)
        {
            switch (CompressionFormat)
            {
                case CompressionFormat.Deflate:
                    using (var stream = new DeflateStream(MemoryStreamManager.GetStream((byte[])value), CompressionMode.Decompress))
                    {
                        return Serializer.Deserialize<T>(stream);
                    }
                case CompressionFormat.GZip:
                    using (var stream = new GZipStream(MemoryStreamManager.GetStream((byte[])value), CompressionMode.Decompress))
                    {
                        return Serializer.Deserialize<T>(stream);
                    }
                case CompressionFormat.None:
                default:
                    return Serializer.Deserialize<T>(value);
            }
        }

        public RedisValue Serialize<T>(T instance)
        {
            switch (CompressionFormat)
            {
                case CompressionFormat.Deflate:
                    using (var memStream = MemoryStreamManager.GetStream())
                    {
                        using (var stream = new DeflateStream(memStream, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            Serializer.Serialize(stream, instance);
                        }
                        return RedisValue.CreateFrom(memStream);
                    }
                case CompressionFormat.GZip:
                    using (var memStream = MemoryStreamManager.GetStream())
                    {
                        using (var stream = new GZipStream(memStream, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            Serializer.Serialize(stream, instance);
                        }
                        return RedisValue.CreateFrom(memStream);
                    }
                case CompressionFormat.None:
                default:
                    using (var stream = MemoryStreamManager.GetStream())
                    {
                        Serializer.Serialize(stream, instance);
                        return RedisValue.CreateFrom(stream);
                    }
            };
        }
    }
}
