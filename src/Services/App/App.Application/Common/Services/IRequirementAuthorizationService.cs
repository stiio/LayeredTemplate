using LayeredTemplate.App.Domain.Common;
using LayeredTemplate.App.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Application.Common.Services;

public interface IRequirementAuthorizationService
{
    Task<AuthorizationResult> Authorize<TResource>(Guid resourceId, params RequirementAction[] actions)
        where TResource : class, IBaseEntity;
}