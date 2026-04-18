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
    /// <summary>
    /// <para>Built-in scopes that every instance must have.</para>
    /// <para>
    /// <b>Resources</b> matter for microservices: when a scope has resources attached, every
    /// access_token issued with that scope carries the resource URI in <c>aud</c>. The downstream
    /// service then configures <c>ValidAudience = "api://my-service"</c> in JwtBearer, so tokens
    /// intended for other services are rejected.
    /// </para>
    /// <para>
    /// OIDC-standard identity scopes (openid/profile/email/phone/roles/offline_access) are not
    /// tied to a resource — they describe the user, not an API. Custom API scopes should name
    /// their resource explicitly (see <c>AppScopes.AdminUsers</c> below for the template pattern).
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<ScopeSeed> BuiltInScopes =
    [
        new("openid", "OpenID"),
        new("profile", "Profile"),
        new("email", "Email"),
        new("phone", "Phone"),
        new("offline_access", "Offline access (refresh tokens)"),
        new(AppScopes.Roles, "User roles"),

        // API scope for Auth.Web's own admin endpoints. Resources make access_token.aud include
        // this URI, matching the validation handler configured in JwtBearer on admin controllers.
        new(AppScopes.AdminUsers, "Admin: manage users", "api://auth-web"),

        // Future API scopes example — each service gets its own resource URI:
        // new("app/all.read",  "Read from App",   "api://app-web"),
        // new("app/all.write", "Modify App data", "api://app-web"),
        // new("reports/all.read",   "Access reports",  "api://reports-web"),
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

        foreach (var seed in BuiltInScopes)
        {
            if (await this.scopeManager.FindByNameAsync(seed.Name, cancellationToken) is not null)
            {
                continue;
            }

            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = seed.Name,
                DisplayName = seed.DisplayName,
            };

            foreach (var resource in seed.Resources)
            {
                descriptor.Resources.Add(resource);
            }

            await this.scopeManager.CreateAsync(descriptor, cancellationToken);

            this.logger.LogInformation(
                "Seeded OIDC scope {Scope} (resources: {Resources}).",
                seed.Name,
                seed.Resources.Length == 0 ? "none" : string.Join(", ", seed.Resources));
        }
    }

    private sealed record ScopeSeed(string Name, string DisplayName, params string[] Resources);
}
