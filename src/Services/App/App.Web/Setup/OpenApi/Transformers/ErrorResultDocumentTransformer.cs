using System.Text.Json;
using LayeredTemplate.App.Shared.Errors.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Ensures <see cref="AppProblemDetails"/> + <see cref="AppErrorType"/> schemas are registered
/// once in the document — referenced from per-operation 400/500 responses by
/// <see cref="DefaultApplicationResponsesTransformer"/>.
/// </summary>
internal sealed class ErrorResultDocumentTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        if (!document.Components.Schemas.ContainsKey(nameof(AppErrorType)))
        {
            document.Components.Schemas.Add(nameof(AppErrorType), new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = Enum.GetNames<AppErrorType>().Select(name => JsonSerializer.SerializeToNode(name)!).ToList(),
            });
        }

        if (!document.Components.Schemas.ContainsKey(nameof(AppProblemDetails)))
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(AppProblemDetails), cancellationToken: cancellationToken);
            if (schema.Properties?["status"] is OpenApiSchema statusProperty)
            {
                statusProperty.Pattern = null;
                statusProperty.Type = JsonSchemaType.Integer | JsonSchemaType.Null;
            }

            schema.Properties!["errorType"] = new OpenApiSchemaReference(nameof(AppErrorType));

            document.Components.Schemas.Add(nameof(AppProblemDetails), schema);
        }
    }
}
