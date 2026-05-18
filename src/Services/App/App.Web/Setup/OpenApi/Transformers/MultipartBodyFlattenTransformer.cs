using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Normalises <c>multipart/form-data</c> request body schemas produced by Minimal API:
/// <list type="bullet">
/// <item>Flattens the <c>allOf</c>-of-singletons composition Minimal API emits when binding
///   <see cref="Microsoft.AspNetCore.Http.IFormFile"/> + <c>[FromForm]</c> primitives — each
///   parameter ends up as its own <c>allOf</c> branch with a single property; we lift those
///   into the top-level <c>properties</c>.</item>
/// <item>Camel-cases every property name in the body schema, the <c>required</c> set, and the
///   <c>encoding</c> keys — so the spec matches the convention already enforced for query /
///   route / header parameters by <see cref="CamelCaseParametersTransformer"/>.</item>
/// </list>
/// Idempotent — re-running on an already-flat, already-camelCased schema is a no-op.
/// </summary>
/// <remarks>
/// Register <i>last</i> among operation transformers so it observes the final shape produced by
/// other multipart-aware transformers (<c>MultiPartJsonOperationTransformer</c>,
/// <c>AsParametersRequiredFixer</c>). Body parts those transformers added in either casing get
/// normalised here in one pass.
/// </remarks>
internal sealed class MultipartBodyFlattenTransformer : IOpenApiOperationTransformer
{
    private const string MultipartContentType = "multipart/form-data";

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content is not { } content)
        {
            return Task.CompletedTask;
        }

        if (!content.TryGetValue(MultipartContentType, out var media) || media.Schema is not OpenApiSchema schema)
        {
            return Task.CompletedTask;
        }

        FlattenAllOf(schema);
        CamelCaseProperties(schema);
        CamelCaseRequired(schema);
        CamelCaseEncoding(media);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Lifts properties out of every <c>allOf</c> branch into the top-level <c>properties</c> bag.
    /// Only branches that look like plain inline objects (no <c>$ref</c>, no nested composition)
    /// are merged — anything else is left in place so we don't accidentally lose structure.
    /// </summary>
    private static void FlattenAllOf(OpenApiSchema schema)
    {
        if (schema.AllOf is not { Count: > 0 } allOf)
        {
            return;
        }

        var remaining = new List<IOpenApiSchema>();

        foreach (var branch in allOf)
        {
            if (branch is OpenApiSchema concrete && IsFlattenable(concrete))
            {
                if (concrete.Properties is { Count: > 0 })
                {
                    schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                    foreach (var (name, propSchema) in concrete.Properties)
                    {
                        // Earlier-added top-level properties win — preserves whatever a more
                        // specific transformer (e.g. MultiPartJsonOperationTransformer) put there.
                        schema.Properties.TryAdd(name, propSchema);
                    }
                }

                if (concrete.Required is { Count: > 0 })
                {
                    schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                    foreach (var name in concrete.Required)
                    {
                        schema.Required.Add(name);
                    }
                }
            }
            else
            {
                remaining.Add(branch);
            }
        }

        schema.AllOf = remaining.Count > 0 ? remaining : null;
    }

    /// <summary>
    /// A schema is safe to flatten if it's a plain inline object — no <c>$ref</c>, no further
    /// composition (<c>allOf/oneOf/anyOf</c>). Branches that don't qualify stay in place.
    /// </summary>
    private static bool IsFlattenable(OpenApiSchema branch) =>
        branch.AllOf is null or { Count: 0 } &&
        branch.OneOf is null or { Count: 0 } &&
        branch.AnyOf is null or { Count: 0 };

    private static void CamelCaseProperties(OpenApiSchema schema)
    {
        if (schema.Properties is not { Count: > 0 } props)
        {
            return;
        }

        var renamed = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach (var (name, value) in props)
        {
            renamed[ToCamel(name)] = value;
        }

        schema.Properties = renamed;
    }

    private static void CamelCaseRequired(OpenApiSchema schema)
    {
        if (schema.Required is not { Count: > 0 } required)
        {
            return;
        }

        schema.Required = new HashSet<string>(required.Select(ToCamel), StringComparer.Ordinal);
    }

    private static void CamelCaseEncoding(OpenApiMediaType media)
    {
        if (media.Encoding is not { Count: > 0 } encoding)
        {
            return;
        }

        var renamed = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal);
        foreach (var (name, value) in encoding)
        {
            renamed[ToCamel(name)] = value;
        }

        media.Encoding = renamed;
    }

    private static string ToCamel(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);
}
