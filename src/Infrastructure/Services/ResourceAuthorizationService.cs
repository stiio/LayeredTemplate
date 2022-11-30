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

    public async Task Authorize<TResource>(TResource resource, IAuthorizationRequirement requirement)
        where TResource : class
    {
        if (this.htContextAccessor.HttpContext?.User == null)
        {
            this.logger.LogInformation("Resource authorization failed: {resource}", resource.GetType().Name);
            throw new AccessDeniedException();
        }

        var result = await this.authorizationService.AuthorizeAsync(this.htContextAccessor.HttpContext.User, resource, requirement);

        if (!result.Succeeded)
        {
            this.logger.LogInformation("Resource authorization failed: {resource}", resource.GetType().Name);
            throw new AccessDeniedException();
        }

        this.logger.LogInformation("Resource authorization succeed: {resource}", resource.GetType().Name);
    }

    public async Task Authorize<TResource>(TResource resource, IEnumerable<IAuthorizationRequirement> requirements)
        where TResource : class
    {
        if (this.htContextAccessor.HttpContext?.User == null)
        {
            this.logger.LogInformation("Resource authorization failed: {resource}", resource.GetType().Name);
            throw new AccessDeniedException();
        }

        var result = await this.authorizationService.AuthorizeAsync(this.htContextAccessor.HttpContext.User, resource, requirements);

        if (!result.Succeeded)
        {
            this.logger.LogInformation("Resource authorization failed: {resource}", resource.GetType().Name);
            throw new AccessDeniedException();
        }

        this.logger.LogInformation("Resource authorization succeed: {resource}", resource.GetType().Name);
    }
}