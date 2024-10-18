using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.Users.Services;
using LayeredTemplate.Domain.Common;
using LayeredTemplate.Infrastructure.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Infrastructure.Authorization.Handlers;

public class HasPermissionRequirementHandler<T> : AuthorizationHandler<HasPermissionRequirement<T>>
    where T : class, IBaseEntity
{
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentUserService currentUserService;
    private readonly IHttpContextAccessor httpContextAccessor;

    public HasPermissionRequirementHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
        this.httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, HasPermissionRequirement<T> requirement)
    {
        if (this.httpContextAccessor.HttpContext!.User.Identity?.IsAuthenticated != true)
        {
            context.Fail();
            return;
        }
        
        if (requirement.ResourceId is null)
        {
            context.Succeed(requirement);
            return;
        }

        var resource = await this.dbContext.Set<T>().AsNoTracking().FirstByIdOrDefault(requirement.ResourceId);
        if (resource is null)
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}