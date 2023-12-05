using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace LayeredTemplate.Infrastructure.AuthorizationHandlers;

internal class ApiKeyAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ApiKey>
{
    private readonly ICurrentUserService currentUserService;

    public ApiKeyAuthorizationHandler(ICurrentUserService currentUserService)
    {
        this.currentUserService = currentUserService;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ApiKey resource)
    {
        if (resource.UserId == this.currentUserService.UserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}