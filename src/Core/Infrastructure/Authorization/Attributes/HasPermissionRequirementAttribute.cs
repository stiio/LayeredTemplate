using System.Text;
using LayeredTemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Infrastructure.Authorization.Attributes;

public class HasPermissionRequirementAttribute<T> : AuthorizeAttribute
    where T : class
{
    public HasPermissionRequirementAttribute(
        string resourceIdElementName,
        BindFrom bindFrom,
        params RequirementAction[] actions)
        : base()
    {
        this.Policy = GetPolicyName(resourceIdElementName, bindFrom, actions);
    }

    private static string GetPolicyName(
        string resourceIdElementName,
        BindFrom bindFrom,
        RequirementAction[] actions)
    {
        var sb = new StringBuilder();
        sb.Append(AuthorizeConstants.PolicyPrefix.HasPermissionRequirement);
        sb.Append(":");
        sb.Append(string.Join(",", actions));
        sb.Append(":");
        sb.Append(bindFrom);
        sb.Append(":");
        sb.Append(resourceIdElementName);
        sb.Append(":");
        sb.Append(typeof(T).FullName);
        sb.Append(", ");
        sb.Append(typeof(T).Assembly.GetName().Name);

        return sb.ToString();
    }
}