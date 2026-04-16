using System.Security.Cryptography;
using System.Text.Json;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

/// <summary>
/// Rotates OpenIddict signing and encryption keys on application startup.
///
/// Key lifecycle (default 90-day rotation, 180-day max age):
///   Day 0:   Key A created (active)
///   Day 90:  Key A still valid, Key B created (active). Both in JWKS.
///   Day 180: Key A removed. Key B active, Key C created.
///
/// Keys older than <see cref="MaxKeyAgeDays"/> are deleted.
/// A new key is created if no key exists or all keys are older than <see cref="RotationIntervalDays"/>.
/// After rotation, all active keys are loaded into <see cref="SigningKeyStore"/> for OpenIddict to consume.
///
/// KeyData is encrypted at rest using ASP.NET Data Protection.
/// </summary>
public class RotateSigningKeysTask : IStartupTask
{
    private const int RotationIntervalDays = 90;
    private const int MaxKeyAgeDays = 180;
    private const string SigningPurpose = "signing";
    private const string EncryptionPurpose = "encryption";
    private const string DataProtectionPurpose = "OpenIddict.SigningCredentials";

    private readonly AuthDbContext dbContext;
    private readonly SigningKeyStore keyStore;
    private readonly IDataProtector protector;
    private readonly ILogger<RotateSigningKeysTask> logger;
    private readonly ILockProvider lockProvider;

    public RotateSigningKeysTask(
        AuthDbContext dbContext,
        SigningKeyStore keyStore,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<RotateSigningKeysTask> logger,
        ILockProvider lockProvider)
    {
        this.dbContext = dbContext;
        this.keyStore = keyStore;
        this.protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        this.logger = logger;
        this.lockProvider = lockProvider;
    }

    public int Order => 30;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using (await this.lockProvider.AcquireLockAsync(
                   "rotate-signing-keys",
                   timeout: TimeSpan.FromSeconds(60),
                   cancellationToken: cancellationToken))
        {
            await this.RotateKeyAsync(SigningPurpose, cancellationToken);
            await this.RotateKeyAsync(EncryptionPurpose, cancellationToken);
        }

        // Load all active keys into the singleton store for OpenIddict
        var allKeys = await this.dbContext.SigningCredentials
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var key in allKeys.Where(k => k.Purpose == SigningPurpose))
        {
            this.keyStore.SigningKeys.Add(this.DeserializeKey(key.KeyData, key.Id.ToString()));
        }

        foreach (var key in allKeys.Where(k => k.Purpose == EncryptionPurpose))
        {
            this.keyStore.EncryptionKeys.Add(this.DeserializeKey(key.KeyData, key.Id.ToString()));
        }

        this.logger.LogInformation(
            "Loaded {SigningCount} signing and {EncryptionCount} encryption key(s) into store.",
            this.keyStore.SigningKeys.Count,
            this.keyStore.EncryptionKeys.Count);
    }

    private async Task RotateKeyAsync(string purpose, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-MaxKeyAgeDays);

        // Delete expired keys
        var expired = await this.dbContext.SigningCredentials
            .Where(k => k.Purpose == purpose && k.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            this.dbContext.SigningCredentials.RemoveRange(expired);
            await this.dbContext.SaveChangesAsync(cancellationToken);
            this.logger.LogInformation("Removed {Count} expired {Purpose} key(s).", expired.Count, purpose);
        }

        // Check if rotation is needed
        var newest = await this.dbContext.SigningCredentials
            .Where(k => k.Purpose == purpose)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (newest is not null && (now - newest.CreatedAt).TotalDays < RotationIntervalDays)
        {
            this.logger.LogInformation(
                "Current {Purpose} key is {Age} days old. No rotation needed.",
                purpose,
                (int)(now - newest.CreatedAt).TotalDays);
            return;
        }

        // Generate new RSA key and encrypt before storing
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);
        var keyJson = JsonSerializer.Serialize(RsaKeyData.FromParameters(parameters));
        var encryptedKeyData = this.protector.Protect(keyJson);

        var credential = new SigningCredential
        {
            Id = Guid.CreateVersion7(),
            KeyData = encryptedKeyData,
            Purpose = purpose,
            CreatedAt = now,
        };

        this.dbContext.SigningCredentials.Add(credential);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Created new {Purpose} key {KeyId}.", purpose, credential.Id);
    }

    private RsaSecurityKey DeserializeKey(string encryptedKeyData, string keyId)
    {
        var keyJson = this.protector.Unprotect(encryptedKeyData);
        var data = JsonSerializer.Deserialize<RsaKeyData>(keyJson)!;
        var rsa = RSA.Create();
        rsa.ImportParameters(data.ToRSAParameters());
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }

    /// <summary>Serializable RSA key parameters.</summary>
    internal sealed class RsaKeyData
    {
        public byte[] Modulus { get; set; } = default!;

        public byte[] Exponent { get; set; } = default!;

        public byte[] D { get; set; } = default!;

        public byte[] P { get; set; } = default!;

        public byte[] Q { get; set; } = default!;

        public byte[] DP { get; set; } = default!;

        public byte[] DQ { get; set; } = default!;

        public byte[] InverseQ { get; set; } = default!;

        public static RsaKeyData FromParameters(RSAParameters p) => new()
        {
            Modulus = p.Modulus!,
            Exponent = p.Exponent!,
            D = p.D!,
            P = p.P!,
            Q = p.Q!,
            DP = p.DP!,
            DQ = p.DQ!,
            InverseQ = p.InverseQ!,
        };

        public RSAParameters ToRSAParameters() => new()
        {
            Modulus = this.Modulus,
            Exponent = this.Exponent,
            D = this.D,
            P = this.P,
            Q = this.Q,
            DP = this.DP,
            DQ = this.DQ,
            InverseQ = this.InverseQ,
        };
    }
}
