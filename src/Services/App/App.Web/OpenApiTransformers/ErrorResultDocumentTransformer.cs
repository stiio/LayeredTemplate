using LayeredTemplate.App.Application.Common.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class ErrorResultDocumentTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();

        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        if (!document.Components.Schemas.ContainsKey(nameof(AppProblemDetails)))
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(AppProblemDetails), cancellationToken: cancellationToken);
            var statusProperty = schema.Properties!["status"] as OpenApiSchema;
            statusProperty!.Pattern = null;
            statusProperty.Type = JsonSchemaType.Integer | JsonSchemaType.Null;

            document.Components.Schemas.Add(nameof(AppProblemDetails), schema);
        }
    }
}
