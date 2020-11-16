using ProtoBuf;
using StackExchange.Redis;
using System.IO;
using System.IO.Compression;
using System.ServiceModel.Channels;

namespace IntelligentHack.IntelligentCache
{
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
                default:
                    return Serializer.Deserialize<T>(value);
            }
        }

        public RedisValue Serialize<T>(T instance)
        {
            switch (CompressionFormat)
            {
                case CompressionFormat.Deflate:
                    using (var memStream = new MemoryStream())
                    {
                        using var stream = new DeflateStream(memStream, CompressionLevel.Optimal);
                        Serializer.Serialize(stream, instance);
#if NETCOREAPP
                        stream.Flush();
                        return RedisValue.CreateFrom(memStream);
#else
                        stream.Close();
                        return RedisValue.CreateFrom(new MemoryStream(memStream.ToArray()));
#endif
                    }
                case CompressionFormat.GZip:
                    using (var memStream = new MemoryStream())
                    {
                        using var stream = new GZipStream(memStream, CompressionLevel.Optimal);
                        Serializer.Serialize(stream, instance);
#if NETCOREAPP
                        stream.Flush();
                        return RedisValue.CreateFrom(memStream);
#else
                        stream.Close();
                        return RedisValue.CreateFrom(new MemoryStream(memStream.ToArray()));
#endif
                    }
                case CompressionFormat.None:
                default:
                    using (var stream = new MemoryStream())
                    {
                        Serializer.Serialize(stream, instance);
                        return RedisValue.CreateFrom(stream);
                    }
            };
        }
    }
}
