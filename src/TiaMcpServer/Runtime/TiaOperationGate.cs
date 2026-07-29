using System;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.Runtime
{
    internal sealed class TiaOperationGate : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task<TResult> RunAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation().ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
