using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class PolymorphismOneOfTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Discriminator is null || schema.AnyOf is not { Count: > 0 })
        {
            return;
        }

        schema.OneOf = schema.AnyOf;
        schema.AnyOf = null;

        if (schema.Discriminator.Mapping is { Count: > 0 })
        {
            var newMapping = new Dictionary<string, OpenApiSchemaReference>();
            foreach (var item in context.JsonTypeInfo.PolymorphismOptions!.DerivedTypes)
            {
                var mappingSchema = await context.GetOrCreateSchemaAsync(item.DerivedType, cancellationToken: cancellationToken);
                newMapping.Add(item.TypeDiscriminator!.ToString()!, new OpenApiSchemaReference(mappingSchema.Metadata!["x-schema-id"].ToString()!));
            }

            schema.Discriminator.Mapping = newMapping;
        }
    }
}
