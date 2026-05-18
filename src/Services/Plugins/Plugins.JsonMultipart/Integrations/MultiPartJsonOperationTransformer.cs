using System.Reflection;
using LayeredTemplate.Plugins.JsonMultipart.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.Plugins.JsonMultipart.Integrations;

/// <summary>
/// Injects <see cref="IJsonMultipartPart{TSelf}"/>-typed handler parameters into the OpenAPI
/// <c>multipart/form-data</c> body schema and attaches <c>contentType: application/json</c>
/// encoding hints so Scalar / codegen render those parts as JSON documents.
/// </summary>
/// <remarks>
/// <para>Minimal API auto-describes <see cref="IFormFile"/> and <c>[FromForm]</c> primitives in the
/// multipart body schema, but it has no way to describe a parameter bound via a custom
/// <c>BindAsync</c> — it sees the type as "unbindable from body" and silently omits it. This
/// transformer fills the gap: for each handler parameter whose type implements
/// <see cref="IJsonMultipartPart{TSelf}"/>, we add a property entry to the body schema (referencing
/// the type's component schema) and an <c>encoding</c> hint.</para>
/// <para>Supports both call styles: top-level handler parameters (recommended) and
/// <c>[AsParameters]</c> aggregate properties (works but the aggregate path is fragile when
/// mixing custom-bound types with native form binding).</para>
/// </remarks>
internal sealed class MultiPartJsonOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content is not { } content ||
            !content.TryGetValue("multipart/form-data", out var multipart))
        {
            return;
        }

        var jsonPartParams = FindJsonPartParameters(context).ToArray();
        if (jsonPartParams.Length == 0)
        {
            return;
        }

        // The multipart schema produced by Minimal API may be either an inline object or a
        // composition (allOf). Either way it carries a `Properties` dictionary at the top — extend
        // it with our JSON parts. Same for `Required`.
        var schema = multipart.Schema as OpenApiSchema;
        if (schema is null)
        {
            return;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        multipart.Encoding ??= new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal);

        foreach (var (fieldName, type, isRequired) in jsonPartParams)
        {
            var camelName = fieldName.ToCamelCase();

            if (!schema.Properties.ContainsKey(camelName))
            {
                // GetOrCreateSchemaAsync builds the schema for the type and stashes the id our
                // CreateSchemaReferenceId callback assigned in `.Metadata["x-schema-id"]` (same
                // pattern as PolymorphismOneOfTransformer). It does NOT automatically register
                // the schema into Document.Components.Schemas — that registration only happens
                // when something else in the document already references the type. Since our
                // injection is the first / only reference here, we register the component
                // explicitly so the `$ref` we emit below resolves to a real schema in OpenAPI
                // codegen output (named `CreateTodoListFileBody` etc., not anonymous).
                var generated = await context.GetOrCreateSchemaAsync(type, cancellationToken: cancellationToken);
                var schemaId = generated.Metadata?["x-schema-id"]?.ToString();

                if (schemaId is not null)
                {
                    context.Document!.Components ??= new OpenApiComponents();
                    context.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                    context.Document.Components.Schemas.TryAdd(schemaId, generated);

                    schema.Properties[camelName] = new OpenApiSchemaReference(schemaId);
                }
                else
                {
                    // No schema id (primitive-like) — inline.
                    schema.Properties[camelName] = generated;
                }
            }

            if (isRequired)
            {
                schema.Required.Add(camelName);
            }

            multipart.Encoding[camelName] = new OpenApiEncoding { ContentType = "application/json" };
        }
    }

    /// <summary>
    /// Yields (fieldName, type, isRequired) for every handler parameter (or <c>[AsParameters]</c>
    /// aggregate property) whose type implements <see cref="IJsonMultipartPart{TSelf}"/>.
    /// </summary>
    private static IEnumerable<(string Name, Type Type, bool Required)> FindJsonPartParameters(OpenApiOperationTransformerContext context)
    {
        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (methodInfo is null)
        {
            yield break;
        }

        foreach (var param in methodInfo.GetParameters())
        {
            // Top-level handler parameter.
            if (IsJsonMultipartPart(param.ParameterType))
            {
                var required = !param.HasDefaultValue
                    && (Nullable.GetUnderlyingType(param.ParameterType) is null);
                yield return (param.Name!, param.ParameterType, required);
                continue;
            }

            // [AsParameters] aggregate — recurse into properties.
            if (param.GetCustomAttribute<AsParametersAttribute>() is not null)
            {
                foreach (var prop in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsJsonMultipartPart(prop.PropertyType))
                    {
                        var required = prop.GetCustomAttributes()
                            .Any(a => a.GetType().Name == "RequiredAttribute");
                        yield return (prop.Name, prop.PropertyType, required);
                    }
                }
            }
        }
    }

    private static bool IsJsonMultipartPart(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJsonMultipartPart<>));
}
