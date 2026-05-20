using Medallion.Threading;

namespace LayeredTemplate.App.Shared.Infrastructure.Locks;

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(LockKey lockKey, TimeSpan? timeout = default, CancellationToken cancellationToken = default);
}
