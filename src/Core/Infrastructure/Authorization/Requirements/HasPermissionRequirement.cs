using LayeredTemplate.Domain.Common;
using LayeredTemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Infrastructure.Authorization.Requirements;

public class HasPermissionRequirement<T> : IAuthorizationRequirement
    where T : class, IBaseEntity
{
    public HasPermissionRequirement(RequirementAction action, Guid? resourceId)
    {
        this.Action = action;
        this.ResourceId = resourceId;
    }

    public RequirementAction Action { get; }

    public Guid? ResourceId { get; }
}