using LayeredTemplate.Application.Common.Interfaces;
using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.Extensions.Configuration;

namespace LayeredTemplate.Infrastructure.Services;

internal class PostgresLockProvider : ILockProvider
{
    private readonly string connectionString;

    public PostgresLockProvider(IConfiguration configuration)
    {
        this.connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<IDistributedSynchronizationHandle> AcquireLockAsync(string name, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        var @lock = new PostgresDistributedLock(new PostgresAdvisoryLockKey(name, true), this.connectionString);

        var handler = await @lock.AcquireAsync(timeout, cancellationToken);

        return handler;
    }
}