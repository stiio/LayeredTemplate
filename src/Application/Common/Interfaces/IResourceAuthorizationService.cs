using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IResourceAuthorizationService
{
    Task Authorize<TResource>(TResource resource, IAuthorizationRequirement requirement)
        where TResource : class;

    Task Authorize<TResource>(TResource resource, IEnumerable<IAuthorizationRequirement> requirements)
        where TResource : class;
}