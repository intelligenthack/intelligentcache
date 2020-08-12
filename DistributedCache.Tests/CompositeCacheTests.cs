using AutoFixture;
using IntelligentHack.DistributedCache;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DistributedCache.Tests
{
    public class CompositeCacheTests
    {
        private readonly Fixture _fixture = new Fixture();

        public CompositeCacheTests()
        {
            TestCache.OperationCounter = 0;
        }

        [Fact]
        public async Task GetSetAsync_favours_the_primary()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var primary = new TestCache();
            var secondary = new TestCache();

            var sut = new CompositeCache(primary, secondary);

            // Act
            var result = await sut.GetSet(key, () => value);

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(primary.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Equal(secondary.Operations, new[] { (1, nameof(TestCache.GetSetAsync), key) });
        }

        [Fact]
        public async Task GetSetAsync_uses_only_the_primary_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var primary = new TestCache { { key, value } };
            var secondary = new TestCache();

            var sut = new CompositeCache(primary, secondary);

            // Act
            var result = await sut.GetSet<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(primary.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Empty(secondary.Operations);
        }

        [Fact]
        public async Task GetSetAsync_uses_value_from_the_secondary_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var primary = new TestCache();
            var secondary = new TestCache { { key, value } };

            var sut = new CompositeCache(primary, secondary);

            // Act
            var result = await sut.GetSet<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(primary.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Equal(secondary.Operations, new[] { (1, nameof(TestCache.GetSetAsync), key) });
        }

        [Fact]
        public async Task Invalidate_starts_from_the_secondary()
        {
            // Arrange
            var key = _fixture.Create<string>();

            var primary = new TestCache();
            var secondary = new TestCache();

            var sut = new CompositeCache(primary, secondary);

            // Act
            await sut.Invalidate(key);

            // Assert
            Assert.Equal(secondary.Operations, new[] { (0, nameof(TestCache.Invalidate), key) });
            Assert.Equal(primary.Operations, new[] { (1, nameof(TestCache.Invalidate), key) });
        }

        public sealed class TestCache : Dictionary<string, object>, ICache
        {
            public static int OperationCounter;

            public event Action<string>? KeyInvalidated;

            public List<(int order, string name, string key)> Operations { get; } = new List<(int, string, string)>();

            public async ValueTask<T> GetSetAsync<T>(string key, Func<ValueTask<T>> calculateValue, TimeSpan duration)
            {
                Operations.Add((OperationCounter++, nameof(GetSetAsync), key));

                if (!TryGetValue(key, out var value))
                {
                    value = await calculateValue();
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
