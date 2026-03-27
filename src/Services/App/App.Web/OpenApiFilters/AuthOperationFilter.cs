using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

/// <inheritdoc />
public class AuthOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>()
            .ToArray() ?? [];

        if (authAttributes.Length == 0)
        {
            return;
        }

        operation.Responses ??= [];
        operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

        operation.Security = new List<OpenApiSecurityRequirement>();
        if (authAttributes.Any(x => x.AuthenticationSchemes == null || x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.Bearer)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.Bearer)] = [],
            });
        }

        if (authAttributes.Any(x => x.AuthenticationSchemes != null && x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.ApiKey)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.ApiKey)] = [],
            });
        }
    }
}