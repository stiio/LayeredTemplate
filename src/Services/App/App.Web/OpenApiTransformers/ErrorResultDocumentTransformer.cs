using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class ErrorResultDocumentTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();

        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        if (!document.Components.Schemas.ContainsKey(nameof(ErrorResult)))
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(ErrorResult), cancellationToken: cancellationToken);
            document.Components.Schemas.Add(nameof(ErrorResult), schema);
        }
    }
}
