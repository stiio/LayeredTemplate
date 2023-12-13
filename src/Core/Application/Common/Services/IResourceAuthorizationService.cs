using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Application.Common.Services;

public interface IResourceAuthorizationService
{
    Task<AuthorizationResult> Authorize<TResource>(TResource resource, IAuthorizationRequirement requirement)
        where TResource : class;

    Task<AuthorizationResult> Authorize<TResource>(TResource resource, IEnumerable<IAuthorizationRequirement> requirements)
        where TResource : class;
}