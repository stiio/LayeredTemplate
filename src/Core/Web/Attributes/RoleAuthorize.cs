using LayeredTemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Web.Attributes;

/// <summary>
/// Role Authorize
/// </summary>
public class RoleAuthorize : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoleAuthorize"/> class.
    /// </summary>
    /// <param name="roles"></param>
    public RoleAuthorize(params Role[] roles)
    {
        if (roles.Any())
        {
            this.Roles = string.Join(", ", roles);
        }
    }
}