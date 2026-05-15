using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.App.Shared.Auth;

/// <summary>
/// Resolves <see cref="HasPermissionAttribute"/>-encoded policy names (<c>HasPermission:X,Y</c>)
/// into <see cref="AuthorizationPolicy"/> at request time. Falls back to the default provider
/// for unknown policy names (so named policies registered via <c>AddAuthorization(x =&gt; ...)</c>
/// still work).
/// </summary>
internal sealed class HasPermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackProvider;

    public HasPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        this.fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult<AuthorizationPolicy?>(BuildHasPermissionPolicy(policyName));
        }

        return this.fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => this.fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => this.fallbackProvider.GetFallbackPolicyAsync();

    private static AuthorizationPolicy BuildHasPermissionPolicy(string policyName)
    {
        var permissions = policyName.Split(':')[1].Split(',');
        if (permissions.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policyName), "Permissions cannot be empty.");
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == AppClaims.Permissions && permissions.Contains(c.Value)))
            .Build();
    }
}
