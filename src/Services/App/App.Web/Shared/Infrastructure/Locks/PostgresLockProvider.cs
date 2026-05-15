using LayeredTemplate.App.Shared.Options;
using Medallion.Threading;
using Medallion.Threading.Postgres;

namespace LayeredTemplate.App.Shared.Infrastructure.Locks;

internal sealed class PostgresLockProvider : ILockProvider
{
    private readonly string connectionString;

    public PostgresLockProvider(IConfiguration configuration)
    {
        this.connectionString = configuration[ConnectionStringKeys.WriteDb]!;
    }

    public async Task<IDistributedSynchronizationHandle> AcquireLockAsync(
        LockKey lockKey,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(20);

        var @lock = new PostgresDistributedLock(
            new PostgresAdvisoryLockKey(lockKey.Name, true),
            this.connectionString);

        return await @lock.AcquireAsync(timeout, cancellationToken);
    }
}
