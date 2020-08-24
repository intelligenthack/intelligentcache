using AutoFixture;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using IntelligentHack.DistributedCache;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DistributedCache.PerformanceTests
{
    public class RedisValueSerializerBenchmark
    {
        private static readonly Fixture _fixture = new Fixture();
        private readonly RedisValue _int = _fixture.Create<int>();
        private readonly RedisValue _ulong = _fixture.Create<ulong>();
        private readonly RedisValue _double = _fixture.Create<double>();
        private readonly RedisValue _uint = _fixture.Create<uint>();
        private readonly RedisValue _long = _fixture.Create<long>();
        private readonly RedisValue _bool = _fixture.Create<bool>();
        private readonly RedisValue _string = _fixture.Create<string>();

        [Benchmark]
        public void WellKnownConversions()
        {
            var serializer = DefaultRedisValueSerializer.Instance;
            serializer.Deserialize<int>(_int);
            serializer.Deserialize<ulong>(_ulong);
            serializer.Deserialize<double>(_double);
            serializer.Deserialize<uint>(_uint);
            serializer.Deserialize<long>(_long);
            serializer.Deserialize<bool>(_bool);
            serializer.Deserialize<string>(_string);
        }

        [Benchmark]
        public void WellKnownTypes()
        {
            var serializer = DefaultRedisValueSerializer2.Instance;
            serializer.Deserialize<int>(_int);
            serializer.Deserialize<ulong>(_ulong);
            serializer.Deserialize<double>(_double);
            serializer.Deserialize<uint>(_uint);
            serializer.Deserialize<long>(_long);
            serializer.Deserialize<bool>(_bool);
            serializer.Deserialize<string>(_string);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<RedisValueSerializerBenchmark>();
        }
    }
}
