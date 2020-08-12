using AutoFixture;
using IntelligentHack.DistributedCache;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DistributedCache.Tests
{
    public class MemoryCacheTests
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public async Task Item_is_calculated_when_the_cache_is_empty()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            // Act
            var addedValue = await sut.GetSet(key, () => counter++);

            // Assert
            Assert.Equal(0, addedValue);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task Item_is_reused_when_the_cache_contains_it()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var initialValue = await sut.GetSet(key, () => counter++);

            // Act
            var cachedValue = await sut.GetSet(key, () => counter++);

            // Assert
            Assert.Equal(initialValue, cachedValue);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task Item_is_recalculated_after_it_expires()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;
            var expiration = TimeSpan.FromSeconds(_fixture.Create<uint>());

            var initialValue = await sut.GetSet(key, () => counter++, expiration);
            clock.AdvanceBy(expiration);

            // Act
            var recalculatedValue = await sut.GetSet(key, () => counter++);

            // Assert
            Assert.Equal(1, recalculatedValue);
            Assert.Equal(2, counter);
        }

        [Fact]
        public async Task Item_is_recalculated_after_it_is_invalidated()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var initialValue = await sut.GetSet(key, () => counter++);
            await sut.Invalidate(key);

            // Act
            var recalculatedValue = await sut.GetSet(key, () => counter++);

            // Assert
            Assert.Equal(1, recalculatedValue);
            Assert.Equal(2, counter);
        }

        [Fact]
        public async Task Concurrent_requests_reuse_the_same_item_calculation()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var firstAccess = sut.GetSetAsync<int>(key, calculation);
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => counter++);

            Assert.False(firstAccess.IsCompleted);
            Assert.False(secondAccess.IsCompleted);

            calculation.Complete();

            var results = await Task.WhenAll(firstAccess.AsTask(), secondAccess.AsTask());

            // Assert
            Assert.Equal(calculation.Result, results[0]);
            Assert.Equal(calculation.Result, results[1]);
            Assert.Equal(1, calculation.Evaluations);
            Assert.Equal(0, counter);
        }

        [Fact]
        public async Task Invalidation_overrides_pending_calculation()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var firstAccess = sut.GetSetAsync<int>(key, calculation);
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => counter++);

            await sut.Invalidate(key);

            var recalculatedValue = await sut.GetSet(key, () => counter++);

            calculation.Complete();

            var invalidatedResults = await Task.WhenAll(firstAccess.AsTask(), secondAccess.AsTask());

            // Assert
            Assert.Equal(calculation.Result, invalidatedResults[0]);
            Assert.Equal(calculation.Result, invalidatedResults[1]);
            Assert.Equal(1, calculation.Evaluations);
            Assert.Equal(0, recalculatedValue);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task Exceptions_are_not_cached()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var firstAccess = sut.GetSetAsync<int>(key, calculation);
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => counter++);

            calculation.Fail();

            var recalculatedValue = await sut.GetSet(key, () => counter++);

            // Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => firstAccess.AsTask());

            // The second access should also throw because when the request started it was
            // not known that the result would be an exception.
            await Assert.ThrowsAsync<InvalidOperationException>(() => secondAccess.AsTask());

            Assert.Equal(1, calculation.Evaluations);
            Assert.Equal(0, recalculatedValue);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task Timeouts_work_like_exceptions()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();
            var counter = 0;

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var timeout = sut.GetSetAsync(key, async () =>
            {
                await Task.Delay(1000, new CancellationTokenSource(10).Token);
                return counter++;
            });

            await Assert.ThrowsAsync<TaskCanceledException>(() => timeout.AsTask());

            var recalculatedValue = await sut.GetSet(key, () => counter++);

            // Assert
            Assert.Equal(0, recalculatedValue);
            Assert.Equal(1, counter);
        }

        public sealed class LongRunningCalculation
        {
            private readonly TaskCompletionSource<int> _calculation = new TaskCompletionSource<int>();
            private readonly TaskCompletionSource<object?> _evaluation = new TaskCompletionSource<object?>();
            private int _evaluations = 0;

            public int Result { get; }
            public int Evaluations => _evaluations;

            public LongRunningCalculation(int result)
            {
                Result = result;
            }

            /// <summary>
            /// Completes the calculation, releasing the calculation task
            /// </summary>
            public void Complete()
            {
                _calculation.SetResult(Result);
            }

            /// <summary>
            /// Makes the calculation fail with InvalidOperationException
            /// </summary>
            public void Fail()
            {
                _calculation.SetException(new InvalidOperationException());
            }

            /// <summary>
            /// Waits until we are inside the calculation
            /// </summary>
            public async Task WaitForEvaluation() => await _evaluation.Task;

            private async ValueTask<int> Calculate()
            {
                Interlocked.Increment(ref _evaluations);
                _evaluation.TrySetResult(null);
                return await _calculation.Task;
            }

            public static implicit operator Func<ValueTask<int>>(LongRunningCalculation value) => value.Calculate;
        }

        public sealed class TestClock : IClock
        {
            public DateTime UtcNow { get; private set; } = new DateTime(2020, 7, 1, 12, 0, 0, DateTimeKind.Utc);

            public TestClock AdvanceBy(TimeSpan offset)
            {
                UtcNow += offset;
                return this;
            }

            public TestClock AdvanceBySeconds(int seconds) => AdvanceBy(TimeSpan.FromSeconds(seconds));
        }
    }
}
