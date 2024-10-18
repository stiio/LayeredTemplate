using LayeredTemplate.App.Application.Common.Models;
using Medallion.Threading;

namespace LayeredTemplate.App.Application.Common.Services;

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(LockKey lockKey, TimeSpan? timeout = default, CancellationToken cancellationToken = default);
}