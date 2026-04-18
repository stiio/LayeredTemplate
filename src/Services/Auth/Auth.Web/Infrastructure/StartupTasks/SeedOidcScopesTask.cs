using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Plugins.StartupRunner.Services;
using OpenIddict.Abstractions;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

/// <summary>
/// Seeds built-in OIDC scopes into the OpenIddict scope store so they can be
/// picked from checkboxes in the admin dashboard and requested by clients.
/// </summary>
public class SeedOidcScopesTask : IStartupTask
{
    private static readonly IReadOnlyList<(string Name, string DisplayName)> BuiltInScopes =
    [
        ("openid", "OpenID"),
        ("profile", "Profile"),
        ("email", "Email"),
        ("phone", "Phone"),
        ("offline_access", "Offline access (refresh tokens)"),
        (AppScopes.Roles, "User roles"),
        (AppScopes.AdminUsers, "Admin: manage users"),
    ];

    private readonly IOpenIddictScopeManager scopeManager;
    private readonly ILockProvider lockProvider;
    private readonly ILogger<SeedOidcScopesTask> logger;

    public SeedOidcScopesTask(
        IOpenIddictScopeManager scopeManager,
        ILockProvider lockProvider,
        ILogger<SeedOidcScopesTask> logger)
    {
        this.scopeManager = scopeManager;
        this.lockProvider = lockProvider;
        this.logger = logger;
    }

    public int Order => 35;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var @lock = await this.lockProvider.AcquireLockAsync(
            "seed-oidc-scopes",
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        foreach (var (name, displayName) in BuiltInScopes)
        {
            if (await this.scopeManager.FindByNameAsync(name, cancellationToken) is null)
            {
                await this.scopeManager.CreateAsync(
                    new OpenIddictScopeDescriptor { Name = name, DisplayName = displayName },
                    cancellationToken);

                this.logger.LogInformation("Seeded OIDC scope {Scope}.", name);
            }
        }
    }
}
