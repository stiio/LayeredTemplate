using LayeredTemplate.Application.Common.Models;
using LayeredTemplate.Application.Common.Services;
using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.Extensions.Configuration;

namespace LayeredTemplate.Infrastructure.Services.Common;

internal class PostgresLockProvider : ILockProvider
{
    private readonly string connectionString;

    public PostgresLockProvider(IConfiguration configuration)
    {
        this.connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<IDistributedSynchronizationHandle> AcquireLockAsync(LockKey lockKey, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(20);

        var @lock = new PostgresDistributedLock(new PostgresAdvisoryLockKey(lockKey.Name, true), this.connectionString);

        var handler = await @lock.AcquireAsync(timeout, cancellationToken);

        return handler;
    }
}