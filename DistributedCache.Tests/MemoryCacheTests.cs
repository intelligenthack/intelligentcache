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

            await calculation.Complete();

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

            await calculation.Complete();

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
            var firstAccess = sut.GetSetAsync<int>(key, calculation).AsTask();
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => counter++);

            await calculation.Fail();

            // We need to wait for the first access to complete.
            // Before that we can't be sure that the cache had a chance to remove the result from the cache.
            await firstAccess.ContinueWith(_ => { });

            var recalculatedValue = await sut.GetSet(key, () => counter++);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => firstAccess);

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

            // Act
            var timeout = sut.GetSetAsync(key, async ct =>
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

        [Fact]
        public async Task Cancellation_is_honoured_when_calculating_the_value()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var cancellation = new CancellationTokenSource();
            var cancelled = sut.GetSetAsync<int>(key, calculation, cancellationToken: cancellation.Token);
            await calculation.WaitForEvaluation();

            cancellation.Cancel();
            
            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => cancelled.AsTask());
        }

        [Fact]
        public async Task Cancellation_of_the_calculation_is_honoured_when_waiting_for_another_threads_calculation()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var cancellation = new CancellationTokenSource();
            var firstAccess = sut.GetSetAsync<int>(key, calculation, cancellationToken: cancellation.Token);
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => _fixture.Create<int>());

            cancellation.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => firstAccess.AsTask());
            await Assert.ThrowsAsync<TaskCanceledException>(() => secondAccess.AsTask());
        }

        [Fact]
        public async Task Cancellation_is_honoured_when_waiting_for_another_threads_calculation_is_cancelled()
        {
            // Arrange
            var clock = new TestClock();
            var sut = new MemoryCache(clock);

            var key = _fixture.Create<string>();

            var calculation = new LongRunningCalculation(_fixture.Create<int>());

            // Act
            var cancellation = new CancellationTokenSource();
            var firstAccess = sut.GetSetAsync<int>(key, calculation);
            await calculation.WaitForEvaluation();

            var secondAccess = sut.GetSet(key, () => _fixture.Create<int>(), cancellationToken: cancellation.Token);

            cancellation.Cancel();
            await calculation.Complete();

            // Assert
            Assert.Equal(calculation.Result, await firstAccess.AsTask());
            await Assert.ThrowsAsync<OperationCanceledException>(() => secondAccess.AsTask());
        }

        public sealed class LongRunningCalculation
        {
            private readonly TaskCompletionSource<int> _calculation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<object?> _evaluation = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            public Task Complete()
            {
                _calculation.TrySetResult(Result);
                return WaitForCalculation();
            }

            /// <summary>
            /// Makes the calculation fail with InvalidOperationException
            /// </summary>
            public Task Fail()
            {
                _calculation.TrySetException(new InvalidOperationException("Simulated failure"));
                return WaitForCalculation();
            }

            private Task WaitForCalculation()
            {
                // Swallow exceptions
                return _calculation.Task.ContinueWith(t => { });
            }

            /// <summary>
            /// Waits until we are inside the calculation
            /// </summary>
            public async Task WaitForEvaluation() => await _evaluation.Task;

            private async ValueTask<int> Calculate(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _evaluations);
                _evaluation.TrySetResult(null);

                cancellationToken.Register(() => _calculation.TrySetCanceled());
                return await _calculation.Task;
            }

            public static implicit operator Func<CancellationToken, ValueTask<int>>(LongRunningCalculation value) => value.Calculate;
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
