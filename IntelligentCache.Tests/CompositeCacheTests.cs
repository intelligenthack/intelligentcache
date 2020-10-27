using AutoFixture;
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
        private readonly Fixture _fixture = new Fixture();

        public CompositeCacheTests()
        {
            TestCache.OperationCounter = 0;
        }

        [Fact]
        public async Task GetSetAsync_favours_level1()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache();
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = await sut.GetSet(key, () => value);

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSet), key) });
            Assert.Equal(level2.Operations, new[] { (1, nameof(TestCache.GetSet), key) });
        }

        [Fact]
        public async Task GetSetAsync_uses_only_level1_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache { { key, value } };
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = await sut.GetSet<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSet), key) });
            Assert.Empty(level2.Operations);
        }

        [Fact]
        public async Task GetSetAsync_uses_value_from_level2_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache();
            var level2 = new TestCache { { key, value } };

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = await sut.GetSet<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSet), key) });
            Assert.Equal(level2.Operations, new[] { (1, nameof(TestCache.GetSet), key) });
        }

        [Fact]
        public async Task Invalidate_starts_from_level2()
        {
            // Arrange
            var key = _fixture.Create<string>();

            var level1 = new TestCache();
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            await sut.Invalidate(key);

            // Assert
            Assert.Equal(level2.Operations, new[] { (0, nameof(TestCache.Invalidate), key) });
            Assert.Equal(level1.Operations, new[] { (1, nameof(TestCache.Invalidate), key) });
        }

        public sealed class TestCache : Dictionary<string, object>, ICache
        {
            public static int OperationCounter;

            public event Action<string>? KeyInvalidated;

            public List<(int order, string name, string key)> Operations { get; } = new List<(int, string, string)>();

            public async ValueTask<T> GetSet<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken)
            {
                Operations.Add((OperationCounter++, nameof(GetSet), key));

                if (!TryGetValue(key, out var value))
                {
                    value = await calculateValue(cancellationToken);
                }
                return (T)value!;
            }

            public ValueTask Invalidate(string key)
            {
                Operations.Add((OperationCounter++, nameof(Invalidate), key));

                Remove(key);
                KeyInvalidated?.Invoke(key);
                return default;
            }
        }
    }
}
