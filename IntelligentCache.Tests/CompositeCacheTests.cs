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
            var result = await sut.GetSetAsync(key, () => value);

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Equal(level2.Operations, new[] { (1, nameof(TestCache.GetSetAsync), key) });
        }

        [Fact]
        public void GetSet_favours_level1()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache();
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet(key, () => value);

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
            var result = await sut.GetSetAsync<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Empty(level2.Operations);
        }

        [Fact]
        public void GetSet_uses_only_level1_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache { { key, value } };
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet<int>(key, () => throw new InvalidOperationException());

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
            var result = await sut.GetSetAsync<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSetAsync), key) });
            Assert.Equal(level2.Operations, new[] { (1, nameof(TestCache.GetSetAsync), key) });
        }

        [Fact]
        public void GetSet_uses_value_from_level2_when_it_contains_a_value()
        {
            // Arrange
            var key = _fixture.Create<string>();
            var value = _fixture.Create<int>();

            var level1 = new TestCache();
            var level2 = new TestCache { { key, value } };

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet<int>(key, () => throw new InvalidOperationException());

            // Assert
            Assert.Equal(value, result);
            Assert.Equal(level1.Operations, new[] { (0, nameof(TestCache.GetSet), key) });
            Assert.Equal(level2.Operations, new[] { (1, nameof(TestCache.GetSet), key) });
        }

        [Fact]
        public async Task InvalidateAsync_starts_from_level2()
        {
            // Arrange
            var key = _fixture.Create<string>();

            var level1 = new TestCache();
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            await sut.InvalidateAsync(key);

            // Assert
            Assert.Equal(level2.Operations, new[] { (0, nameof(TestCache.InvalidateAsync), key) });
            Assert.Equal(level1.Operations, new[] { (1, nameof(TestCache.InvalidateAsync), key) });
        }

        [Fact]
        public void Invalidate_starts_from_level2()
        {
            // Arrange
            var key = _fixture.Create<string>();

            var level1 = new TestCache();
            var level2 = new TestCache();

            var sut = new CompositeCache(level1, level2);

            // Act
            sut.Invalidate(key);

            // Assert
            Assert.Equal(level2.Operations, new[] { (0, nameof(TestCache.Invalidate), key) });
            Assert.Equal(level1.Operations, new[] { (1, nameof(TestCache.Invalidate), key) });
        }

        public sealed class TestCache : Dictionary<string, object>, ICache
        {
            public static int OperationCounter;

            public List<(int order, string name, string key)> Operations { get; } = new List<(int, string, string)>();

            public async ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValueAsync, TimeSpan duration, CancellationToken cancellationToken)
            {
                Operations.Add((OperationCounter++, nameof(GetSetAsync), key));

                if (!TryGetValue(key, out var value))
                {
                    value = await calculateValueAsync(cancellationToken);
                }
                return (T)value!;
            }

            public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration)
            {
                Operations.Add((OperationCounter++, nameof(GetSet), key));

                if (!TryGetValue(key, out var value))
                {
                    value = calculateValue();
                }
                return (T)value!;
            }

            public ValueTask InvalidateAsync(string key, bool wasTriggeredLocally = true, CancellationToken cancellationToken = default)
            {
                Operations.Add((OperationCounter++, nameof(InvalidateAsync), key));
                Remove(key);
                return default;
            }

            public void Invalidate(string key, bool wasTriggeredLocally = true)
            {
                Operations.Add((OperationCounter++, nameof(Invalidate), key));
                Remove(key);
            }
        }
    }
}
