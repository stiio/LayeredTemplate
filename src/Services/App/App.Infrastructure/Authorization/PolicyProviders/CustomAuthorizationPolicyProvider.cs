using LayeredTemplate.Plugins.Authorization.Abstractions.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.App.Infrastructure.Authorization.PolicyProviders;

public class CustomAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackPolicyProvider;

    public CustomAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options)
    {
        this.fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AuthorizeConstants.PolicyPrefix.HasPermission))
        {
            return Task.FromResult<AuthorizationPolicy?>(this.GetHasPermissionPolicy(policyName));
        }

        return this.fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return this.fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return this.fallbackPolicyProvider.GetFallbackPolicyAsync();
    }

    private AuthorizationPolicy GetHasPermissionPolicy(string policyName)
    {
        var permissions = policyName.Split(":")[1].Split(',');
        if (permissions.Length == 0)
        {
            throw new ArgumentOutOfRangeException($"{nameof(permissions)} cannot cannot be empty.");
        }

        var policy = new AuthorizationPolicyBuilder();

        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(x =>
        {
            return x.User.HasClaim(c => c.Type == AppClaims.Permissions && permissions.Contains(c.Value));
        });

        return policy.Build();
    }
}