using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentHack.IntelligentCache
{
    internal class MultiKeyLock: IDisposable
    {
        private readonly ConcurrentDictionary<string, ReaderWriterLockSlim> Map = new ConcurrentDictionary<string, ReaderWriterLockSlim>();
        private bool disposedValue;

        private void SafeOp(string key, Action<ReaderWriterLockSlim> action)
        {
            var thelock = Map.GetOrAdd(key, (_) => new ReaderWriterLockSlim());
            action(thelock);
        }
        public void EnterWriteLock(string key) => SafeOp(key, (r) => r.EnterWriteLock());
        public void EnterReadLock(string key) => SafeOp(key, (r) => r.EnterReadLock());
        public void EnterUpgradeableReadLock(string key) => SafeOp(key, (r) => r.EnterUpgradeableReadLock());
        public void ExitWriteLock(string key) => SafeOp(key, (r) => r.ExitWriteLock());
        public void ExitReadLock(string key) => SafeOp(key, (r) => r.ExitReadLock());
        public void ExitUpgradeableReadLock(string key) => SafeOp(key, (r) => r.ExitUpgradeableReadLock());

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var thelock in Map.Values)
                    {
                        thelock.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
