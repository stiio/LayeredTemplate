using LayeredTemplate.Plugins.Authorization.Abstractions.Constants;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class SecurityDefinitionTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[AppAuthenticationSchemes.Bearer] = new OpenApiSecurityScheme()
        {
            Name = AppAuthenticationSchemes.Bearer,
            BearerFormat = "JWT",
            Scheme = AppAuthenticationSchemes.Bearer,
            Description = "Specify the authorization token.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
        };

        document.Components.SecuritySchemes[AppAuthenticationSchemes.ApiKey] = new OpenApiSecurityScheme()
        {
            Name = "X-Api-Key",
            Description = "Specify the api key.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
        };

        return Task.CompletedTask;
    }
}
