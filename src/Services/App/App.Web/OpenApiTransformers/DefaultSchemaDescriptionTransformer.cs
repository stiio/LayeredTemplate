using Humanizer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class DefaultSchemaDescriptionTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        schema.Description ??= context.JsonPropertyInfo?.Name?.Humanize()
                               ?? (context.JsonTypeInfo.Type.IsGenericType ? null : context.JsonTypeInfo.Type.Name.Humanize());

        return Task.CompletedTask;
    }
}
