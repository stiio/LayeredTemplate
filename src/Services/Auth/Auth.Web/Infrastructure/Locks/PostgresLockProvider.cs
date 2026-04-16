using LayeredTemplate.Auth.Web.Infrastructure.Options.Constants;
using Medallion.Threading;
using Medallion.Threading.Postgres;

namespace LayeredTemplate.Auth.Web.Infrastructure.Locks;

internal class PostgresLockProvider : ILockProvider
{
    private readonly string connectionString;

    public PostgresLockProvider(IConfiguration configuration)
    {
        this.connectionString = configuration[ConnectionStrings.AuthDbConnection]!;
    }

    public async Task<IDistributedSynchronizationHandle> AcquireLockAsync(string lockKey, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(20);

        var @lock = new PostgresDistributedLock(new PostgresAdvisoryLockKey(lockKey, allowHashing: true), this.connectionString);

        var handler = await @lock.AcquireAsync(timeout, cancellationToken);

        return handler;
    }
}