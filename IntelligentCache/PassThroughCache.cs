using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that always calls the <paramref name="calculateValue"/> callback.
    /// </summary>
    /// <remarks>
    /// This class provides a "null object" implementation of <see cref="ICache" />.
    /// It can be useful in tests or other contexts that require a cache.
    /// </remarks>
    public sealed class PassThroughCache : ICache
    {
        /// <inheritdoc />
        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return calculateValue();
        }

        /// <inheritdoc />
        public Task<T> GetSetAsync<T>(string key, Func<CancellationToken, Task<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return calculateValue(cancellationToken);
        }

        /// <inheritdoc />
        public void Invalidate(string key)
        {
            return;
        }

        /// <inheritdoc />
        public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
