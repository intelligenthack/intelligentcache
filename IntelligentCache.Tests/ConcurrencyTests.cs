// Suppress xUnit warnings about Task.WaitAll - these are intentional for testing synchronous methods concurrently
#pragma warning disable xUnit1031

using IntelligentHack.IntelligentCache;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class ConcurrencyTests
    {
        private static int _nextCachePrefixId;

        private static string GeneratePrefix()
        {
            var prefixId = Interlocked.Increment(ref _nextCachePrefixId);
            return $"concurrency-test{prefixId}";
        }

        [Fact]
        public void GetSet_concurrent_calls_same_key_calculates_value_only_once()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var calculationCount = 0;
            var startBarrier = new Barrier(10);
            var results = new ConcurrentBag<string>();

            // Act - 10 threads all trying to get the same key simultaneously
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                startBarrier.SignalAndWait(); // Ensure all threads start at the same time
                var result = sut.GetSet("shared-key", () =>
                {
                    Interlocked.Increment(ref calculationCount);
                    Thread.Sleep(50); // Simulate slow calculation
                    return "calculated-value";
                }, TimeSpan.FromMinutes(1));
                results.Add(result);
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert - calculateValue should only be called once (thundering herd prevention)
            Assert.Equal(1, calculationCount);
            Assert.All(results, r => Assert.Equal("calculated-value", r));
        }

        [Fact]
        public async Task GetSetAsync_concurrent_calls_same_key_calculates_value_only_once()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var calculationCount = 0;
            var startBarrier = new Barrier(10);

            // Act - 10 concurrent async calls for the same key
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
            {
                startBarrier.SignalAndWait();
                return await sut.GetSetAsync("shared-key", async ct =>
                {
                    Interlocked.Increment(ref calculationCount);
                    await Task.Delay(50, ct);
                    return "calculated-value";
                }, TimeSpan.FromMinutes(1));
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, calculationCount);
            Assert.All(results, r => Assert.Equal("calculated-value", r));
        }

        [Fact]
        public void GetSet_concurrent_calls_different_keys_all_calculate()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var calculationCount = 0;
            var startBarrier = new Barrier(10);

            // Act - 10 threads each getting a different key
            var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                return sut.GetSet($"key-{i}", () =>
                {
                    Interlocked.Increment(ref calculationCount);
                    return $"value-{i}";
                }, TimeSpan.FromMinutes(1));
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert - each key should trigger its own calculation
            Assert.Equal(10, calculationCount);
        }

        [Fact]
        public void GetSet_concurrent_invalidate_during_read_does_not_deadlock()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            sut.GetSet("key", () => "initial", TimeSpan.FromMinutes(1));

            var completed = false;
            var startBarrier = new Barrier(2);

            // Act - one thread reads while another invalidates
            var readTask = Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                {
                    sut.GetSet("key", () => $"value-{i}", TimeSpan.FromMinutes(1));
                }
            });

            var invalidateTask = Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                {
                    sut.Invalidate("key");
                }
            });

            var completedInTime = Task.WaitAll(new[] { readTask, invalidateTask }, TimeSpan.FromSeconds(10));
            completed = completedInTime;

            // Assert - should complete without deadlock
            Assert.True(completed, "Operations should complete without deadlock");
        }

        [Fact]
        public async Task GetSetAsync_concurrent_invalidate_during_read_does_not_deadlock()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            await sut.GetSetAsync("key", ct => Task.FromResult("initial"), TimeSpan.FromMinutes(1));

            var startBarrier = new Barrier(2);

            // Act
            var readTask = Task.Run(async () =>
            {
                startBarrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                {
                    await sut.GetSetAsync("key", ct => Task.FromResult($"value-{i}"), TimeSpan.FromMinutes(1));
                }
            });

            var invalidateTask = Task.Run(async () =>
            {
                startBarrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                {
                    await sut.InvalidateAsync("key");
                }
            });

            var allTasks = Task.WhenAll(readTask, invalidateTask);
            var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(10)));

            // Assert - should complete without deadlock (within timeout)
            Assert.True(completed == allTasks, "Operations should complete without deadlock");
        }
    }
}
