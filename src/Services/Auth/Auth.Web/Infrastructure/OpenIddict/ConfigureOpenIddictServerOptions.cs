using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;

/// <summary>
/// Loads signing and encryption keys from <see cref="SigningKeyStore"/> into OpenIddict server options.
/// The store is populated by RotateSigningKeysTask at startup (before any request is processed).
/// Registered as singleton — no scoped dependencies.
/// </summary>
public class ConfigureOpenIddictServerOptions(SigningKeyStore keyStore, ILogger<ConfigureOpenIddictServerOptions> logger)
    : IPostConfigureOptions<OpenIddictServerOptions>
{
    public void PostConfigure(string? name, OpenIddictServerOptions options)
    {
        if (keyStore.SigningKeys.Count == 0 || keyStore.EncryptionKeys.Count == 0)
        {
            logger.LogWarning("No signing/encryption keys in store. OpenIddict will use development certificates.");
            return;
        }

        foreach (var key in keyStore.SigningKeys)
        {
            options.SigningCredentials.Add(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        }

        foreach (var key in keyStore.EncryptionKeys)
        {
            options.EncryptionCredentials.Add(new EncryptingCredentials(
                key,
                SecurityAlgorithms.RsaOAEP,
                SecurityAlgorithms.Aes256CbcHmacSha512));
        }

        logger.LogInformation(
            "Configured OpenIddict with {SigningCount} signing and {EncryptionCount} encryption key(s).",
            keyStore.SigningKeys.Count,
            keyStore.EncryptionKeys.Count);
    }
}
