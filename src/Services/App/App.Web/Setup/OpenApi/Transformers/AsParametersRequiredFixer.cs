using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Minimal API's OpenAPI generator loses <see cref="RequiredAttribute"/> propagation when handler
/// parameters are aggregated into an <c>[AsParameters]</c> type — required properties end up
/// missing from <c>schema.required</c> (body) and <c>parameters[].required</c> (query/route/header).
/// This transformer walks every handler method parameter, recurses into <c>[AsParameters]</c>
/// aggregates, and re-applies required-ness based on:
/// <list type="bullet">
/// <item>presence of <see cref="RequiredAttribute"/></item>
/// <item>non-nullable value type properties (always required per OpenAPI convention)</item>
/// </list>
/// Idempotent — safe to run alongside other transformers that may already set some required
/// entries (e.g. <c>MultiPartJsonOperationTransformer</c> for JSON-multipart parts).
/// </summary>
internal sealed class AsParametersRequiredFixer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (methodInfo is null)
        {
            return Task.CompletedTask;
        }

        foreach (var param in methodInfo.GetParameters())
        {
            if (param.GetCustomAttribute<AsParametersAttribute>() is null)
            {
                continue;
            }

            foreach (var prop in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsPropertyRequired(prop))
                {
                    continue;
                }

                // Schema property names can be either PascalCase (native binding / [FromForm])
                // or camelCase (JSON-mapped DTOs, CamelCaseParametersTransformer). Try both —
                // matchers only act when the property actually exists in that location, so
                // silently ignoring no-match is correct.
                var camelName = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
                var pascalName = prop.Name;

                MarkRequiredInBodySchemas(operation, camelName, pascalName);
                MarkRequiredInOperationParameter(operation, camelName, pascalName);
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsPropertyRequired(PropertyInfo prop)
    {
        if (prop.GetCustomAttribute<RequiredAttribute>() is not null)
        {
            return true;
        }

        // Non-nullable value types are required by OpenAPI convention — there's no way to send
        // "no bool" over the wire; absence means binding failure.
        var t = prop.PropertyType;
        return t.IsValueType && Nullable.GetUnderlyingType(t) is null;
    }

    private static void MarkRequiredInBodySchemas(OpenApiOperation operation, params string[] candidates)
    {
        if (operation.RequestBody?.Content is not { } content)
        {
            return;
        }

        foreach (var media in content.Values)
        {
            if (media.Schema is not OpenApiSchema schema)
            {
                continue;
            }

            // The property may sit at the top level OR inside one of the allOf branches
            // (Minimal API composes multipart bodies as allOf-of-singletons). Match the first
            // candidate name that's actually present — preserving the casing the spec already uses.
            var matched = candidates.FirstOrDefault(name =>
                PropertyExists(schema, name) || AllOfContainsProperty(schema, name));

            if (matched is null)
            {
                continue;
            }

            schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
            schema.Required.Add(matched);
        }
    }

    private static void MarkRequiredInOperationParameter(OpenApiOperation operation, params string[] candidates)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var p in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (candidates.Any(name => string.Equals(p.Name, name, StringComparison.Ordinal)))
            {
                p.Required = true;
                return;
            }
        }
    }

    private static bool PropertyExists(OpenApiSchema schema, string name) =>
        schema.Properties?.ContainsKey(name) == true;

    private static bool AllOfContainsProperty(OpenApiSchema schema, string name) =>
        schema.AllOf?.OfType<OpenApiSchema>()
            .Any(s => s.Properties?.ContainsKey(name) == true) == true;
}
