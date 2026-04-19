using LayeredTemplate.App.Shared.Authorization;
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
            BearerFormat = "JWT",
            Scheme = AppAuthenticationSchemes.Bearer,
            Description = "Specify the authorization token.",
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
