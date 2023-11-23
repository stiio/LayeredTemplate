using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

/// <inheritdoc />
public class AuthOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>()
            .ToArray() ?? Array.Empty<AuthorizeAttribute>();

        if (authAttributes.Any())
        {
            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

            operation.Security = new List<OpenApiSecurityRequirement>();
            if (authAttributes.Any(x => x.AuthenticationSchemes == null || x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.Bearer)))
            {
                operation.Security.Add(new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = AppAuthenticationSchemes.Bearer },
                        },
                        System.Array.Empty<string>()
                    },
                });
            }

            if (authAttributes.Any(x => x.AuthenticationSchemes != null && x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.ApiKey)))
            {
                operation.Security.Add(new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = AppAuthenticationSchemes.ApiKey },
                        },
                        System.Array.Empty<string>()
                    },
                });
            }
        }
    }
}