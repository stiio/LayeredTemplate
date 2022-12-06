using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Services;

internal class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly IHttpContextAccessor htContextAccessor;
    private readonly IAuthorizationService authorizationService;
    private readonly ILogger<ResourceAuthorizationService> logger;

    public ResourceAuthorizationService(
        IHttpContextAccessor htContextAccessor,
        IAuthorizationService authorizationService,
        ILogger<ResourceAuthorizationService> logger)
    {
        this.htContextAccessor = htContextAccessor;
        this.authorizationService = authorizationService;
        this.logger = logger;
    }

    public Task<AuthorizationResult> Authorize<TResource>(TResource resource, IAuthorizationRequirement requirement)
        where TResource : class
    {
        return this.htContextAccessor.HttpContext?.User == null
            ? Task.FromResult(AuthorizationResult.Failed())
            : this.authorizationService.AuthorizeAsync(this.htContextAccessor.HttpContext.User, resource, requirement);
    }

    public Task<AuthorizationResult> Authorize<TResource>(TResource resource, IEnumerable<IAuthorizationRequirement> requirements)
        where TResource : class
    {
        return this.htContextAccessor.HttpContext?.User == null
            ? Task.FromResult(AuthorizationResult.Failed())
            : this.authorizationService.AuthorizeAsync(this.htContextAccessor.HttpContext.User, resource, requirements);
    }
}