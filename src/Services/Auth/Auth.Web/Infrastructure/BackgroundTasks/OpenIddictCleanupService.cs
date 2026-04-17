using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using OpenIddict.Abstractions;

namespace LayeredTemplate.Auth.Web.Infrastructure.BackgroundTasks;

/// <summary>
/// Periodically prunes expired/revoked OpenIddict tokens and ad-hoc authorizations.
/// Runs under a distributed lock so only one instance performs cleanup per interval.
/// </summary>
public class OpenIddictCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);
    private static readonly TimeSpan PruneThreshold = TimeSpan.FromDays(14);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(15);
    private const string LockKey = "openiddict-cleanup";

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<OpenIddictCleanupService> logger;

    public OpenIddictCleanupService(IServiceScopeFactory scopeFactory, ILogger<OpenIddictCleanupService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let startup tasks (migrations, key rotation, seed) finish before the first cleanup.
            await Task.Delay(InitialDelay, stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await this.RunCleanupAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = this.scopeFactory.CreateAsyncScope();
        var lockProvider = scope.ServiceProvider.GetRequiredService<ILockProvider>();

        try
        {
            await using var @lock = await lockProvider.AcquireLockAsync(
                LockKey,
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken: cancellationToken);

            var threshold = DateTimeOffset.UtcNow - PruneThreshold;
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var authorizationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();

            await tokenManager.PruneAsync(threshold, cancellationToken);
            await authorizationManager.PruneAsync(threshold, cancellationToken);

            this.logger.LogInformation(
                "OpenIddict cleanup completed. Pruned tokens and authorizations older than {Threshold:O}.",
                threshold);
        }
        catch (TimeoutException)
        {
            this.logger.LogInformation("Skipped OpenIddict cleanup: lock held by another instance.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "OpenIddict cleanup failed.");
        }
    }
}
