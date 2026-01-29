using IntelligentHack.IntelligentCache;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class CompositeCacheBackPopulationTests
    {
        private static int _nextCachePrefixId;

        private static string GeneratePrefix()
        {
            var prefixId = Interlocked.Increment(ref _nextCachePrefixId);
            return $"composite-test{prefixId}";
        }

        [Fact]
        public void GetSet_when_L1_misses_and_L2_hits_L1_gets_populated()
        {
            // Arrange
            var l1 = new MemoryCache(GeneratePrefix() + "-l1");
            var l2 = new MemoryCache(GeneratePrefix() + "-l2");

            // Pre-populate L2 only
            l2.GetSet("key", () => "value-from-l2", TimeSpan.FromMinutes(1));

            var sut = new CompositeCache(l1, l2);
            var calculateCalled = false;

            // Act - first call: L1 miss, L2 hit
            var result1 = sut.GetSet("key", () =>
            {
                calculateCalled = true;
                return "should-not-be-called";
            }, TimeSpan.FromMinutes(1));

            // Assert - should get L2's value without calling calculateValue
            Assert.False(calculateCalled, "calculateValue should not be called when L2 hits");
            Assert.Equal("value-from-l2", result1);

            // Act - second call should hit L1 (verify L1 was populated)
            var l1HitCount = 0;
            var l2HitCount = 0;

            // Create new composite with instrumented caches to verify L1 hit
            var l1Instrumented = new InstrumentedCache(l1, () => l1HitCount++);
            var l2Instrumented = new InstrumentedCache(l2, () => l2HitCount++);
            var sutInstrumented = new CompositeCache(l1Instrumented, l2Instrumented);

            var result2 = sutInstrumented.GetSet("key", () => "should-not-be-called", TimeSpan.FromMinutes(1));

            // Assert - L1 should be hit, L2 should not be called
            Assert.Equal("value-from-l2", result2);
            Assert.Equal(1, l1HitCount);
            Assert.Equal(0, l2HitCount);
        }

        [Fact]
        public async Task GetSetAsync_when_L1_misses_and_L2_hits_L1_gets_populated()
        {
            // Arrange
            var l1 = new MemoryCache(GeneratePrefix() + "-l1");
            var l2 = new MemoryCache(GeneratePrefix() + "-l2");

            // Pre-populate L2 only
            await l2.GetSetAsync("key", ct => Task.FromResult("value-from-l2"), TimeSpan.FromMinutes(1));

            var sut = new CompositeCache(l1, l2);
            var calculateCalled = false;

            // Act - first call
            var result1 = await sut.GetSetAsync("key", ct =>
            {
                calculateCalled = true;
                return Task.FromResult("should-not-be-called");
            }, TimeSpan.FromMinutes(1));

            Assert.False(calculateCalled);
            Assert.Equal("value-from-l2", result1);

            // Verify L1 was populated by checking it directly
            var l1Value = await l1.GetSetAsync("key", ct => Task.FromResult("fallback"), TimeSpan.FromMinutes(1));
            Assert.Equal("value-from-l2", l1Value);
        }

        [Fact]
        public void GetSet_when_both_miss_value_is_stored_in_both_levels()
        {
            // Arrange
            var l1 = new MemoryCache(GeneratePrefix() + "-l1");
            var l2 = new MemoryCache(GeneratePrefix() + "-l2");
            var sut = new CompositeCache(l1, l2);

            // Act
            var result = sut.GetSet("key", () => "calculated-value", TimeSpan.FromMinutes(1));

            // Assert - value should be in both caches
            Assert.Equal("calculated-value", result);

            // Verify L1 has the value
            var l1Called = false;
            var l1Value = l1.GetSet("key", () => { l1Called = true; return "l1-fallback"; }, TimeSpan.FromMinutes(1));
            Assert.False(l1Called);
            Assert.Equal("calculated-value", l1Value);

            // Verify L2 has the value
            var l2Called = false;
            var l2Value = l2.GetSet("key", () => { l2Called = true; return "l2-fallback"; }, TimeSpan.FromMinutes(1));
            Assert.False(l2Called);
            Assert.Equal("calculated-value", l2Value);
        }

        /// <summary>
        /// A wrapper cache that tracks when the inner cache is accessed.
        /// </summary>
        private class InstrumentedCache : ICache
        {
            private readonly ICache _inner;
            private readonly Action _onAccess;

            public InstrumentedCache(ICache inner, Action onAccess)
            {
                _inner = inner;
                _onAccess = onAccess;
            }

            public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
            {
                _onAccess();
                return _inner.GetSet(key, calculateValue, duration);
            }

            public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
            {
                _onAccess();
                return _inner.GetSetAsync(key, calculateValue, duration, cancellationToken);
            }

            public void Invalidate(string key)
            {
                _inner.Invalidate(key);
            }

            public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
            {
                return _inner.InvalidateAsync(key, cancellationToken);
            }
        }
    }
}
