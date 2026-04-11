using LayeredTemplate.Plugins.Authorization.Abstractions.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class AuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        if (methodInfo is null)
        {
            return Task.CompletedTask;
        }

        var authAttributes = methodInfo.DeclaringType?.GetCustomAttributes(true)
            .Union(methodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>()
            .ToArray() ?? [];

        if (authAttributes.Length == 0)
        {
            return Task.CompletedTask;
        }

        operation.Responses ??= [];
        operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

        operation.Security = new List<OpenApiSecurityRequirement>();
        if (authAttributes.Any(x => x.AuthenticationSchemes == null || x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.Bearer)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.Bearer, context.Document)] = [],
            });
        }

        if (authAttributes.Any(x => x.AuthenticationSchemes != null && x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.ApiKey)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.ApiKey, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
