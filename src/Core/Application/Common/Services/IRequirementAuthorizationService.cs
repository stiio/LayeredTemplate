using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Application.Common.Services;

public interface IRequirementAuthorizationService
{
    Task<AuthorizationResult> Authorize<TResource>(Guid resourceId, params RequirementAction[] actions)
        where TResource : class, IBaseEntity;
}