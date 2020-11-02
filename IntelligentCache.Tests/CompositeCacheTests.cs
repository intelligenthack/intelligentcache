using IntelligentHack.IntelligentCache;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class CompositeCacheTests
    {
        public class LoggingCache : ICache
        {
            private List<string> log;
            private string name;
            private bool useLambda;

            public LoggingCache(string name, List<string> log, bool useLambda)
            {
                this.log = log;
                this.name = name;
                this.useLambda = useLambda;
            }

            public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
            {
                var res = useLambda?calculateValue():default;
                log.Add($"{name}.{nameof(GetSet)}.{key}={res}");
                return res;
            }

            public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default)
            {
                var res = useLambda?await calculateValue(CancellationToken.None):default;
                log.Add($"{name}.{nameof(GetSetAsync)}.{key}={res}");
                return res;
            }

            public void Invalidate(string key)
            {
                log.Add($"{name}.{nameof(Invalidate)}.{key}");
            }

            public async ValueTask InvalidateAsync(string key)
            {
                log.Add($"{name}.{nameof(InvalidateAsync)}.{key}");
            }
        }


        [Fact]
        public void GetSet_favors_level1()
        {
            // Arrange
            var log = new List<string>();
            var level1 = new LoggingCache("level1", log, true);
            var level2 = new LoggingCache("level2", log, true);

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet("a", () => (int?)null, TimeSpan.Zero);

            // Assert
            Assert.Equal(2,log.Count);
            Assert.Equal("level2.GetSet.a=", log[0]);
            Assert.Equal("level1.GetSet.a=", log[1]);
            Assert.False(result.HasValue);
        }

        [Fact]
        public async Task InvalidateAsync_starts_from_level2()
        {
            // Arrange
            var log = new List<string>();
            var level1 = new LoggingCache("level1", log, false);
            var level2 = new LoggingCache("level2", log, false);

            var sut = new CompositeCache(level1, level2);

            // Act
            await sut.InvalidateAsync("a");

            // Assert
            Assert.Equal(2,log.Count);
            Assert.Equal("level2.InvalidateAsync.a", log[0]);
            Assert.Equal("level1.InvalidateAsync.a", log[1]);
        }

    }
}
