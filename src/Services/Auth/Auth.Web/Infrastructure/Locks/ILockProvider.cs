using Medallion.Threading;

namespace LayeredTemplate.Auth.Web.Infrastructure.Locks;

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(string lockKey, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}