using LayeredTemplate.App.Domain.Common;
using LayeredTemplate.App.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Infrastructure.Authorization.Requirements;

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