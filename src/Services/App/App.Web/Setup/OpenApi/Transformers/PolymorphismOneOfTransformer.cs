using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Fixes up the OpenAPI schema for polymorphic base types produced by <c>System.Text.Json</c>'s
/// source generator:
/// <list type="bullet">
/// <item><b>anyOf → oneOf</b> — STJ emits <c>anyOf + discriminator</c>; OpenAPI clients expect
///   <c>oneOf</c> for tagged unions. We flip the keyword.</item>
/// <item><b>Discriminator mapping with $refs</b> — default mapping values are bare discriminator
///   strings, not schema references; client generators can't resolve them. We rebuild the mapping
///   so each entry points at the derived type's <c>$ref</c>.</item>
/// <item><b>Discriminator property on the base schema</b> — STJ doesn't add the discriminator
///   property (e.g. <c>$type</c>) to <c>properties</c> on the base; only <c>required</c> mentions
///   it. We inject it as a string property with <c>enum</c>-of-all-discriminator-values so client
///   codegen sees a proper union-of-literal-types discriminator instead of an unenumerated string.</item>
/// </list>
/// </summary>
internal sealed class PolymorphismOneOfTransformer : IOpenApiSchemaTransformer
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

        AddDiscriminatorProperty(schema, context);
    }

    /// <summary>
    /// Adds the discriminator property (e.g. <c>$type</c>) to the base schema with a string
    /// <c>enum</c> listing every concrete derived-type tag. Idempotent — if the property is already
    /// present (e.g. a previous transformer pass put it there), we leave it alone.
    /// </summary>
    private static void AddDiscriminatorProperty(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        var propertyName = schema.Discriminator?.PropertyName;
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        if (schema.Properties.ContainsKey(propertyName))
        {
            return;
        }

        var discriminatorValues = context.JsonTypeInfo.PolymorphismOptions!.DerivedTypes
            .Select(d => d.TypeDiscriminator!.ToString()!)
            .ToList();

        schema.Properties[propertyName] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = discriminatorValues
                .Select(v => JsonSerializer.SerializeToNode(v)!)
                .ToList(),
        };
    }
}
