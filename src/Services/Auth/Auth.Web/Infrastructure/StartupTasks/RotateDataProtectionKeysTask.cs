using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

/// <summary>
/// Creates Data Protection keys at startup instead of relying on runtime auto-generation.
/// This eliminates race conditions when multiple instances start simultaneously —
/// each instance would otherwise independently create its own key.
///
/// Should run under a distributed lock in multi-instance deployments.
///
/// Key lifecycle (RotationIntervalDays=320, KeyLifetimeDays=360):
///   Day 0:   Key A created (expires day 360)
///   Day 200: Restart. Key A age 200 &lt; 320 → no rotation
///   Day 325: Restart. Key A age 325 &gt; 320 → Key B created (expires day 685). Key A still decrypts until day 360.
///   Day 500: Restart. Key B age 175 &lt; 320 → no rotation
///   Day 650: Restart. Key B age 325 &gt; 320 → Key C created (expires day 1010). Key A long gone, Key B still decrypts until day 685.
/// Overlap window between rotation (320d) and lifetime (360d) is 40 days — the service must be
/// restarted at least once in that window for the successor key to land before the current one expires.
///
/// AutoGenerateKeys is disabled in KeyManagementOptions, so no keys are created at runtime.
/// </summary>
public class RotateDataProtectionKeysTask : IStartupTask
{
    private const int KeyLifetimeDays = 360;
    private const int RotationIntervalDays = 320;

    private readonly IKeyManager keyManager;
    private readonly ILogger<RotateDataProtectionKeysTask> logger;
    private readonly ILockProvider lockProvider;

    public RotateDataProtectionKeysTask(
        IKeyManager keyManager,
        ILogger<RotateDataProtectionKeysTask> logger,
        ILockProvider lockProvider)
    {
        this.keyManager = keyManager;
        this.logger = logger;
        this.lockProvider = lockProvider;
    }

    public int Order => 20;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var @lock = await this.lockProvider.AcquireLockAsync(
            "rotate-date-protection-keys",
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var allKeys = this.keyManager.GetAllKeys();
        var newest = allKeys
            .Where(k => !k.IsRevoked)
            .OrderByDescending(k => k.CreationDate)
            .FirstOrDefault();

        if (newest is not null)
        {
            var age = (now - newest.CreationDate).TotalDays;
            if (age < RotationIntervalDays)
            {
                this.logger.LogInformation(
                    "Data Protection key {KeyId} is {Age} days old (rotation at {Interval}). No rotation needed.",
                    newest.KeyId,
                    (int)age,
                    RotationIntervalDays);

                return;
            }
        }

        var newKey = this.keyManager.CreateNewKey(
            activationDate: now,
            expirationDate: now.AddDays(KeyLifetimeDays));

        this.logger.LogInformation(
            "Created new Data Protection key {KeyId}, expires {Expiration}.",
            newKey.KeyId,
            newKey.ExpirationDate);
    }
}
