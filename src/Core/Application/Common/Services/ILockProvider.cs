using LayeredTemplate.Application.Common.Models;
using Medallion.Threading;

namespace LayeredTemplate.Application.Common.Services;

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(LockKey lockKey, TimeSpan? timeout = default, CancellationToken cancellationToken = default);
}