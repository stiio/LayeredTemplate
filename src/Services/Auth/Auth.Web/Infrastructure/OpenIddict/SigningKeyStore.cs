using Microsoft.IdentityModel.Tokens;

namespace LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;

/// <summary>
/// Singleton store for signing and encryption keys loaded from the database.
/// Populated by <see cref="StartupTasks.RotateSigningKeysTask"/> at startup,
/// consumed by <see cref="ConfigureOpenIddictServerOptions"/> to configure OpenIddict.
/// </summary>
public class SigningKeyStore
{
    public List<RsaSecurityKey> SigningKeys { get; } = [];

    public List<RsaSecurityKey> EncryptionKeys { get; } = [];
}
