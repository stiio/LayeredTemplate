using System.Text;
using LayeredTemplate.Plugins.Authorization.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Infrastructure.Authorization.Attributes;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(params Permissions[] actions)
    {
        this.Policy = GetPolicyName(actions);
    }

    private static string GetPolicyName(Permissions[] actions)
    {
        var sb = new StringBuilder();
        sb.Append(AuthorizeConstants.PolicyPrefix.HasPermission);
        sb.Append(":");
        sb.Append(string.Join(",", actions));

        return sb.ToString();
    }
}