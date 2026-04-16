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
/// Key lifecycle (RotationIntervalDays=90, KeyLifetimeDays=180):
///   Day 0:   Key A created (expires day 180)
///   Day 50:  Restart. Key A age 50 &lt; 90 → no rotation
///   Day 100: Restart. Key A age 100 &gt; 90 → Key B created (expires day 280). Key A still decrypts.
///   Day 150: Restart. Key B age 50 &lt; 90 → no rotation
///   Day 200: Restart. Key B age 100 &gt; 90 → Key C created (expires day 380). Key A expired, Key B still decrypts.
///
/// AutoGenerateKeys is disabled in KeyManagementOptions, so no keys are created at runtime.
/// </summary>
public class RotateDataProtectionKeysTask : IStartupTask
{
    private const int KeyLifetimeDays = 360;
    private const int RotationIntervalDays = 180;

    private readonly IKeyManager keyManager;
    private readonly ILogger<RotateDataProtectionKeysTask> logger;

    public RotateDataProtectionKeysTask(IKeyManager keyManager,
        ILogger<RotateDataProtectionKeysTask> logger)
    {
        this.keyManager = keyManager;
        this.logger = logger;
    }

    public int Order => 20;

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
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

                return Task.CompletedTask;
            }
        }

        var newKey = this.keyManager.CreateNewKey(
            activationDate: now,
            expirationDate: now.AddDays(KeyLifetimeDays));

        this.logger.LogInformation(
            "Created new Data Protection key {KeyId}, expires {Expiration}.",
            newKey.KeyId,
            newKey.ExpirationDate);

        return Task.CompletedTask;
    }
}
