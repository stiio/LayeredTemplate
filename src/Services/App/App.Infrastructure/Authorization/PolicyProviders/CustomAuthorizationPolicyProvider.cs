using LayeredTemplate.App.Domain.Enums;
using LayeredTemplate.App.Infrastructure.Authorization.Requirements;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.App.Infrastructure.Authorization.PolicyProviders;

public class CustomAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackPolicyProvider;
    private readonly IHttpContextAccessor httpContextAccessor;

    public CustomAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AuthorizeConstants.PolicyPrefix.HasPermissionOnAction))
        {
            return Task.FromResult<AuthorizationPolicy?>(this.GetHasPermissionOnActionPolicy(policyName));
        }
        else if (policyName.StartsWith(AuthorizeConstants.PolicyPrefix.HasPermissionRequirement))
        {
            return Task.FromResult<AuthorizationPolicy?>(this.GetHasPermissionOnResourcePolicy(policyName));
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

    private AuthorizationPolicy GetHasPermissionOnActionPolicy(string policyName)
    {
        var actionNames = policyName.Split(":")[1].Split(',').ToArray();
        if (actionNames.Length == 0)
        {
            throw new ArgumentOutOfRangeException($"{nameof(actionNames)} cannot cannot be empty.");
        }

        var policy = new AuthorizationPolicyBuilder();

        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(x =>
        {
            return x.User.HasClaim(c => c.Type == AppClaims.AllowedActions && actionNames.Contains(c.Value));
        });

        return policy.Build();
    }

    private AuthorizationPolicy GetHasPermissionOnResourcePolicy(string policyName)
    {
        var parts = policyName.Split(":");
        var requirementActions = parts[1].Split(',').ToArray();
        if (requirementActions.Length == 0)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requirementActions)} cannot cannot be empty.");
        }

        var bindFrom = parts[2];
        var resourceElementIdName = parts[3];
        var type = Type.GetType(parts[4])!;

        var policy = new AuthorizationPolicyBuilder();
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(requirementActions.Select(requirementAction =>
        {
            var hasPermissionRequirementType = typeof(HasPermissionRequirement<>).MakeGenericType(type);
            var resourceId = this.GetResourceId(Enum.Parse<BindFrom>(bindFrom), resourceElementIdName);
            return (IAuthorizationRequirement)Activator.CreateInstance(
                hasPermissionRequirementType,
                Enum.Parse<RequirementAction>(requirementAction),
                resourceId)!;
        }).ToArray());

        return policy.Build();
    }

    private Guid? GetResourceId(BindFrom bindFrom, string resourceIdFormElementName)
    {
        switch (bindFrom)
        {
            case BindFrom.Route:
            {
                var rawValue = this.httpContextAccessor.HttpContext!.GetRouteValue(resourceIdFormElementName);
                return Guid.TryParse(rawValue?.ToString(), out var value) ? value : null;
            }

            case BindFrom.Query:
            {
                var rawValue = this.httpContextAccessor.HttpContext!.Request.Query[resourceIdFormElementName].ToString();
                return Guid.TryParse(rawValue, out var value) ? value : null;
            }

            case BindFrom.Form:
            {
                var rawValue = this.httpContextAccessor.HttpContext!.Request.Form[resourceIdFormElementName].ToString();
                return Guid.TryParse(rawValue, out var value) ? value : null;
            }

            default:
            {
                throw new ArgumentException($"Bind from '{bindFrom}' is invalid.", nameof(bindFrom));
            }
        }
    }
}