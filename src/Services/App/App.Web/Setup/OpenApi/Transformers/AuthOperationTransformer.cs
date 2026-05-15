using LayeredTemplate.App.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// For each operation that has any <see cref="IAuthorizeData"/> on its endpoint metadata
/// (from <c>RequireAuthorization()</c> on the group or attribute on the handler), adds 401/403
/// responses and binds the right security scheme. Minimal-API-friendly version of the
/// controller-based transformer that pre-existed.
/// </summary>
internal sealed class AuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var authData = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .ToArray();

        if (authData.Length == 0)
        {
            return Task.CompletedTask;
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

        operation.Security = new List<OpenApiSecurityRequirement>();

        if (authData.Any(x => string.IsNullOrEmpty(x.AuthenticationSchemes) || x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.Bearer)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.Bearer, context.Document)] = [],
            });
        }

        if (authData.Any(x => !string.IsNullOrEmpty(x.AuthenticationSchemes) && x.AuthenticationSchemes.Contains(AppAuthenticationSchemes.ApiKey)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(AppAuthenticationSchemes.ApiKey, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
