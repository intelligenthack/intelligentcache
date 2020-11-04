using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    /// <summary>
    /// An implementation of <see cref="ICache" /> that always calls the <paramref name="calculateValue"/> callback.
    /// </summary>
    public sealed class PassThroughCache : ICache
    {
        public TimeSpan CacheDuration { get; set; }

        public PassThroughCache()
        {
            this.CacheDuration = TimeSpan.MaxValue;
        }

        public T GetSet<T>(string key, Func<T> calculateValue, TimeSpan duration) where T : class
        {
            return calculateValue();

        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, TimeSpan duration, CancellationToken cancellationToken = default) where T : class
        {
            return calculateValue(cancellationToken);
        }

        public void Invalidate(string key)
        {
            return;
        }

        public ValueTask InvalidateAsync(string key)
        {
            return new ValueTask();
        }

        public ValueTask<T> GetSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> calculateValue, CancellationToken cancellationToken = default) where T : class
        {
            return this.GetSetAsync(key,calculateValue,this.CacheDuration,cancellationToken);
        }

        public T GetSet<T>(string key, Func<T> calculateValue) where T : class
        {
            return this.GetSet(key,calculateValue,this.CacheDuration);
        }
    }
}
