using LayeredTemplate.App.Application.Common.Services;
using LayeredTemplate.App.Domain.Common;
using LayeredTemplate.App.Domain.Enums;
using LayeredTemplate.App.Infrastructure.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.App.Infrastructure.Services.Common;

internal class RequirementAuthorizationService : IRequirementAuthorizationService
{
    private readonly IHttpContextAccessor htContextAccessor;
    private readonly IAuthorizationService authorizationService;

    public RequirementAuthorizationService(
        IHttpContextAccessor htContextAccessor,
        IAuthorizationService authorizationService,
        ILogger<RequirementAuthorizationService> logger)
    {
        this.htContextAccessor = htContextAccessor;
        this.authorizationService = authorizationService;
    }

    public Task<AuthorizationResult> Authorize<TResource>(Guid resourceId, params RequirementAction[] actions)
        where TResource : class, IBaseEntity
    {
        if (this.htContextAccessor.HttpContext?.User == null)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }

        var policyBuilder = new AuthorizationPolicyBuilder();
        policyBuilder.RequireAuthenticatedUser();

        var requirements = actions
            .Select(action => new HasPermissionRequirement<TResource>(action, resourceId))
            .ToArray<IAuthorizationRequirement>();

        policyBuilder.AddRequirements(requirements);

        var policy = policyBuilder.Build();

        return this.authorizationService.AuthorizeAsync(this.htContextAccessor.HttpContext.User, policy);
    }
}