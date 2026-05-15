using Medallion.Threading;

namespace LayeredTemplate.App.Shared.Infrastructure.Locks;

public sealed class LockKey
{
    private LockKey(string name)
    {
        this.Name = name;
    }

    public string Name { get; }

    public static LockKey Migrations(string dbContextName) => new($"migrations:{dbContextName}");
}

public interface ILockProvider
{
    Task<IDistributedSynchronizationHandle> AcquireLockAsync(LockKey lockKey, TimeSpan? timeout = default, CancellationToken cancellationToken = default);
}
