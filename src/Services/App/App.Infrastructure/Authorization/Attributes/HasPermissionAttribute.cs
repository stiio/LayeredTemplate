using System.Text;
using LayeredTemplate.App.Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Infrastructure.Authorization.Attributes;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(params AppPermissions[] actions)
    {
        this.Policy = GetPolicyName(actions);
    }

    private static string GetPolicyName(AppPermissions[] actions)
    {
        var sb = new StringBuilder();
        sb.Append(AuthorizeConstants.PolicyPrefix.HasPermission);
        sb.Append(":");
        sb.Append(string.Join(",", actions));

        return sb.ToString();
    }
}