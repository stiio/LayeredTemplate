using Medallion.Threading;

namespace LayeredTemplate.Application.Common.Services;

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(string name, TimeSpan? timeout = default, CancellationToken cancellationToken = default);
}