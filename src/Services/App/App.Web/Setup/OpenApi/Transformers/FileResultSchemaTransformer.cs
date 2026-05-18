using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Renders Minimal API file-result types (<see cref="FileContentHttpResult"/>,
/// <see cref="FileStreamHttpResult"/>, <see cref="PhysicalFileHttpResult"/>,
/// <see cref="VirtualFileHttpResult"/>) as <c>{ type: string, format: binary }</c>, the OpenAPI
/// convention for binary downloads.
/// </summary>
/// <remarks>
/// Without this transformer ASP.NET Core's OpenAPI generator describes these types by their public
/// properties (<c>ContentType</c>, <c>FileDownloadName</c>, …), which is meaningless for clients
/// and produces broken Scalar download UI plus useless TypeScript bindings. The MVC-era equivalent
/// was <c>FileResultTransformer</c> targeting <see cref="Microsoft.AspNetCore.Mvc.FileResult"/>.
/// </remarks>
internal sealed class FileResultSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(FileContentHttpResult) ||
            type == typeof(FileStreamHttpResult) ||
            type == typeof(PhysicalFileHttpResult) ||
            type == typeof(VirtualFileHttpResult))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = "binary";
            schema.Properties = null;
            schema.Required = null;
        }

        return Task.CompletedTask;
    }
}
