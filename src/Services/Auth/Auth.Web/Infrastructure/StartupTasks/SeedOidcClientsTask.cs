using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Plugins.StartupRunner.Services;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

public class SeedOidcClientsTask : IStartupTask
{
    private readonly IOpenIddictApplicationManager manager;
    private readonly ILockProvider lockProvider;

    public SeedOidcClientsTask(IOpenIddictApplicationManager manager, ILockProvider lockProvider)
    {
        this.manager = manager;
        this.lockProvider = lockProvider;
    }

    public int Order => 40;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var @lock = await this.lockProvider.AcquireLockAsync(
            "setup-oidc-clients",
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        if (await this.manager.FindByClientIdAsync("default_client", cancellationToken) is null)
        {
            await this.manager.CreateAsync(
                new OpenIddictApplicationDescriptor
                {
                    ClientId = "default_client",
                    ClientType = ClientTypes.Public,
                    DisplayName = "Default Client",
                    RedirectUris =
                    {
                        new Uri("https://localhost:3062/callback.html"),
                        new Uri("http://localhost:3061/callback.html"),
                        new Uri("https://localhost:7144/authentication/login-callback"),
                        new Uri("http://localhost:5264/authentication/login-callback"),
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("https://localhost:3062/index.html"),
                        new Uri("http://localhost:3061/index.html"),
                        new Uri("https://localhost:7144/authentication/logout-callback"),
                        new Uri("http://localhost:5264/authentication/logout-callback"),
                    },
                    Permissions =
                    {
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.RefreshToken,
                        Permissions.ResponseTypes.Code,
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.Prefixes.Scope + "openid",
                        Permissions.Prefixes.Scope + "profile",
                        Permissions.Prefixes.Scope + "email",
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange,
                    },
                },
                cancellationToken);
        }
    }
}