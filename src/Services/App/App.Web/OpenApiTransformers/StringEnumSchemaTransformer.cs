using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var enumType = Nullable.GetUnderlyingType(type) ?? type;

        if (!enumType.IsEnum)
        {
            return Task.CompletedTask;
        }

        schema.Type = Nullable.GetUnderlyingType(type) is not null
            ? JsonSchemaType.String | JsonSchemaType.Null
            : JsonSchemaType.String;

        schema.Enum = Enum.GetNames(enumType)
            .Select(name => JsonSerializer.SerializeToNode(name)!)
            .ToList();

        return Task.CompletedTask;
    }
}
