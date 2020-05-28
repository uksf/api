using System;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Common {
    public class AsyncLock : IDisposable {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public void Dispose() {
            semaphoreSlim.Release();
        }

        public async Task<AsyncLock> LockAsync() {
            await semaphoreSlim.WaitAsync();
            return this;
        }
    }
}
