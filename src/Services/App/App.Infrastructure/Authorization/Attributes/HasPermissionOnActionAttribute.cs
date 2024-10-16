﻿using System.Text;
using LayeredTemplate.App.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Infrastructure.Authorization.Attributes;

public class HasPermissionOnActionAttribute : AuthorizeAttribute
{
    public HasPermissionOnActionAttribute(params ActionType[] actions)
    {
        this.Policy = GetPolicyName(actions);
    }

    private static string GetPolicyName(ActionType[] actions)
    {
        var sb = new StringBuilder();
        sb.Append(AuthorizeConstants.PolicyPrefix.HasPermissionOnAction);
        sb.Append(":");
        sb.Append(string.Join(",", actions));

        return sb.ToString();
    }
}