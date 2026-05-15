using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Shared.Auth;

/// <summary>
/// Synthetic-policy authorization attribute: encodes a set of required permissions into the
/// policy name (e.g. <c>HasPermission:UserRead,UserWrite</c>) which <see cref="HasPermissionPolicyProvider"/>
/// then dynamically resolves into an <see cref="AuthorizationPolicy"/> at request time. Saves
/// having to register a named policy per permission in <c>AddAuthorization</c>.
/// </summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "HasPermission";

    public HasPermissionAttribute(params AppPermissions[] actions)
    {
        this.Policy = GetPolicyName(actions);
    }

    private static string GetPolicyName(AppPermissions[] actions)
    {
        var sb = new StringBuilder();
        sb.Append(PolicyPrefix);
        sb.Append(':');
        sb.Append(string.Join(",", actions));
        return sb.ToString();
    }
}
