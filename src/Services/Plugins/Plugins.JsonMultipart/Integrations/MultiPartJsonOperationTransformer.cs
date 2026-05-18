using System.Reflection;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;
using LayeredTemplate.Plugins.JsonMultipart.Extensions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.Plugins.JsonMultipart.Integrations;

/// <summary>
/// For endpoints whose request DTO carries any <see cref="FromJsonAttribute"/>-marked properties,
/// adds an <c>encoding</c> entry with <c>contentType: application/json</c> on those parts of the
/// <c>multipart/form-data</c> request body. This hint tells Scalar / OpenAPI codegen to render
/// the field as a JSON document inside the multipart part (vs the default text/plain).
/// </summary>
/// <remarks>
/// Discovers the request type via <see cref="IAcceptsMetadata"/> attached by
/// <see cref="JsonMultipartFormBinder.PopulateMetadata{T}"/> for Minimal API endpoints, with an
/// MVC-era fallback to <c>ActionDescriptor.Parameters</c>. The schema itself is left as the
/// generator produced it (a <c>$ref</c> to the request type's component schema); encoding is
/// applied at the media type level, which works regardless of whether the schema is inline or
/// referenced.
/// </remarks>
internal sealed class MultiPartJsonOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var requestType = ResolveMultipartRequestType(context);
        if (requestType is null)
        {
            return Task.CompletedTask;
        }

        if (!operation.RequestBody!.Content!.TryGetValue("multipart/form-data", out var multipart))
        {
            return Task.CompletedTask;
        }

        var jsonProps = requestType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<FromJsonAttribute>() is not null)
            .ToArray();

        if (jsonProps.Length == 0)
        {
            return Task.CompletedTask;
        }

        multipart.Encoding ??= new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal);

        foreach (var prop in jsonProps)
        {
            multipart.Encoding[prop.Name.ToCamelCase()] = new OpenApiEncoding
            {
                ContentType = "application/json",
            };
        }

        return Task.CompletedTask;
    }

    private static Type? ResolveMultipartRequestType(OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        // Preferred (Minimal API): IAcceptsMetadata attached by JsonMultipartFormBinder.PopulateMetadata.
        var acceptsMetadata = metadata.OfType<IAcceptsMetadata>().FirstOrDefault();
        if (acceptsMetadata?.RequestType is { } acceptsType && HasJsonProperties(acceptsType))
        {
            return acceptsType;
        }

        // Minimal API fallback: walk the handler delegate's parameter types directly. Works even
        // if IEndpointParameterMetadataProvider didn't contribute an IAcceptsMetadata entry
        // (e.g. when DIM doesn't fire for the static abstract method via the marker interface).
        var methodInfo = metadata.OfType<MethodInfo>().FirstOrDefault();
        if (methodInfo is not null)
        {
            foreach (var param in methodInfo.GetParameters())
            {
                if (HasJsonProperties(param.ParameterType))
                {
                    return param.ParameterType;
                }
            }
        }

        // MVC controllers fallback.
        foreach (var p in context.Description.ActionDescriptor.Parameters)
        {
            if (HasJsonProperties(p.ParameterType))
            {
                return p.ParameterType;
            }
        }

        // Final fallback: walk ApiDescription.ParameterDescriptions which Minimal API populates
        // even when MethodInfo isn't surfaced in EndpointMetadata.
        foreach (var p in context.Description.ParameterDescriptions)
        {
            if (p.Type is not null && HasJsonProperties(p.Type))
            {
                return p.Type;
            }
        }

        return null;
    }

    private static bool HasJsonProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttribute<FromJsonAttribute>() is not null);
}
