using LayeredTemplate.Plugins.StartupRunner.Services;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

public class SeedOidcClientsTask : IStartupTask
{
    private readonly IOpenIddictApplicationManager manager;

    public SeedOidcClientsTask(IOpenIddictApplicationManager manager)
    {
        this.manager = manager;
    }

    public int Order => 20;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
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
                        new Uri("https://localhost:7176/callback"),
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("https://localhost:7176/"),
                    },
                    Permissions =
                    {
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.Prefixes.Scope + "openid",
                        Permissions.Prefixes.Scope + "profile",
                        Permissions.Prefixes.Scope + "email",
                        Permissions.Prefixes.Scope + "roles",
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