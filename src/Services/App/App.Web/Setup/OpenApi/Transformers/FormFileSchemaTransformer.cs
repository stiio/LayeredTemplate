using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Renders <see cref="IFormFile"/> as <c>{ type: string, format: binary }</c> per OpenAPI's
/// convention for binary uploads. By default, ASP.NET Core's OpenAPI generator emits an empty
/// object schema for <see cref="IFormFile"/>, which produces a broken upload control in Scalar
/// and bad TypeScript bindings in OpenAPI codegen.
/// </summary>
internal sealed class FormFileSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(IFormFile))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = "binary";
            schema.Properties = null;
            schema.Required = null;
        }

        return Task.CompletedTask;
    }
}
