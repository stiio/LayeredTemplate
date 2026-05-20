using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Marks the polymorphism discriminator (default <c>$type</c>) as <c>required</c> on every
/// <i>derived</i> schema in a <c>[JsonDerivedType]</c> hierarchy. Symmetric to STJ's behaviour on
/// the base schema (where the discriminator is already listed as required).
/// </summary>
/// <remarks>
/// <para>Why a document transformer instead of a schema transformer: Microsoft.AspNetCore.OpenApi
/// (10.0.x) does not invoke <see cref="IOpenApiSchemaTransformer"/> for types that appear in the
/// graph only as derived participants of a polymorphism hierarchy — they're materialised on a
/// dedicated path. Their schemas <i>do</i> end up in <c>Document.Components.Schemas</c>, so a
/// document transformer running after schema generation can find and mutate them.</para>
/// <para>The transformer walks the app assembly for types carrying <see cref="JsonDerivedTypeAttribute"/>,
/// resolves the discriminator property name from <see cref="JsonPolymorphicAttribute"/> (defaulting
/// to <c>$type</c>), then locates each derived type's schema in <c>Components.Schemas</c> by name.
/// Schema id convention here matches <c>ConfigureOpenApi.CreateSchemaReferenceId</c> — for
/// non-nested derived types that's just the type's name; nested derived types would get
/// <c>ParentName + Name</c> (currently unused but supported defensively).</para>
/// </remarks>
internal sealed class PolymorphicDerivedRequiredTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is not { } schemas)
        {
            return Task.CompletedTask;
        }

        var polymorphicBases = typeof(PolymorphicDerivedRequiredTransformer).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false).Any());

        foreach (var baseType in polymorphicBases)
        {
            var discriminatorPropertyName = baseType.GetCustomAttribute<JsonPolymorphicAttribute>()?.TypeDiscriminatorPropertyName ?? "$type";

            foreach (var attr in baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false))
            {
                var schemaId = GetSchemaId(attr.DerivedType);

                if (schemas.TryGetValue(schemaId, out var derivedSchema) && derivedSchema is OpenApiSchema concrete)
                {
                    concrete.Required ??= new HashSet<string>(StringComparer.Ordinal);
                    concrete.Required.Add(discriminatorPropertyName);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Mirrors <c>ConfigureOpenApi.CreateSchemaReferenceId</c>: nested types get
    /// <c>ParentName + Name</c>, top-level types get plain type name.
    /// </summary>
    private static string GetSchemaId(Type t) =>
        t.IsNested && t.DeclaringType is { } parent ? parent.Name + t.Name : t.Name;
}
