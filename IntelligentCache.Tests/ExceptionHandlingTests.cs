// Suppress xUnit warnings about Task.WaitAll - intentional for testing synchronous methods concurrently
#pragma warning disable xUnit1031

using IntelligentHack.IntelligentCache;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public class ExceptionHandlingTests
    {
        private static int _nextCachePrefixId;

        private static string GeneratePrefix()
        {
            var prefixId = Interlocked.Increment(ref _nextCachePrefixId);
            return $"exception-test{prefixId}";
        }

        [Fact]
        public void GetSet_when_calculateValue_throws_exception_is_propagated()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                sut.GetSet<string>("key", () => throw new InvalidOperationException("Test exception"), TimeSpan.FromMinutes(1))
            );
            Assert.Equal("Test exception", ex.Message);
        }

        [Fact]
        public async Task GetSetAsync_when_calculateValue_throws_exception_is_propagated()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await sut.GetSetAsync<string>("key", ct => throw new InvalidOperationException("Test exception"), TimeSpan.FromMinutes(1))
            );
            Assert.Equal("Test exception", ex.Message);
        }

        [Fact]
        public void GetSet_after_exception_cache_remains_functional()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var firstCallMade = false;

            // Act - first call throws
            try
            {
                sut.GetSet<string>("key", () =>
                {
                    firstCallMade = true;
                    throw new InvalidOperationException("First call fails");
                }, TimeSpan.FromMinutes(1));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Second call should work and calculate the value
            var secondCallMade = false;
            var result = sut.GetSet("key", () =>
            {
                secondCallMade = true;
                return "success";
            }, TimeSpan.FromMinutes(1));

            // Assert
            Assert.True(firstCallMade);
            Assert.True(secondCallMade, "Second call should calculate value since first call failed");
            Assert.Equal("success", result);
        }

        [Fact]
        public async Task GetSetAsync_after_exception_cache_remains_functional()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());

            // Act - first call throws
            try
            {
                await sut.GetSetAsync<string>("key", ct => throw new InvalidOperationException("First call fails"), TimeSpan.FromMinutes(1));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Second call should work
            var secondCallMade = false;
            var result = await sut.GetSetAsync("key", ct =>
            {
                secondCallMade = true;
                return Task.FromResult("success");
            }, TimeSpan.FromMinutes(1));

            // Assert
            Assert.True(secondCallMade);
            Assert.Equal("success", result);
        }

        [Fact]
        public void GetSet_exception_does_not_cache_partial_state()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var callCount = 0;

            // Act - multiple calls where first throws
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    sut.GetSet<string>("key", () =>
                    {
                        callCount++;
                        if (callCount < 3)
                            throw new InvalidOperationException($"Call {callCount} fails");
                        return "finally success";
                    }, TimeSpan.FromMinutes(1));
                }
                catch (InvalidOperationException)
                {
                    // Expected for first two calls
                }
            }

            // Assert - should have called calculateValue 3 times (no caching of failed attempts)
            Assert.Equal(3, callCount);

            // Subsequent call should use cached value
            var finalCallMade = false;
            var result = sut.GetSet("key", () =>
            {
                finalCallMade = true;
                return "should not be called";
            }, TimeSpan.FromMinutes(1));

            Assert.False(finalCallMade, "Value should be cached after successful call");
            Assert.Equal("finally success", result);
        }

        [Fact]
        public void GetSet_concurrent_exception_does_not_block_other_threads()
        {
            // Arrange
            var sut = new MemoryCache(GeneratePrefix());
            var barrier = new Barrier(2);
            var thread1Completed = false;
            var thread2Completed = false;

            // Act - two threads, one throws, one succeeds
            var task1 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    sut.GetSet<string>("key1", () =>
                    {
                        Thread.Sleep(50);
                        throw new InvalidOperationException("Thread 1 fails");
                    }, TimeSpan.FromMinutes(1));
                }
                catch (InvalidOperationException)
                {
                    thread1Completed = true;
                }
            });

            var task2 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                sut.GetSet("key2", () =>
                {
                    thread2Completed = true;
                    return "success";
                }, TimeSpan.FromMinutes(1));
            });

            // Use longer timeout for CI environments where thread scheduling may be slower
            var completed = Task.WaitAll(new[] { task1, task2 }, TimeSpan.FromSeconds(30));

            // Assert
            Assert.True(completed, "Both tasks should complete within timeout");
            Assert.True(thread1Completed, "Thread 1 should complete (with exception)");
            Assert.True(thread2Completed, "Thread 2 should complete successfully");
        }
    }
}
