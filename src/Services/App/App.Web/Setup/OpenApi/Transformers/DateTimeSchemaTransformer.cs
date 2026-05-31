using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

internal sealed class DateTimeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(type);
        type = underlying ?? type;

        if (type == typeof(DateOnly))
        {
            schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{DateOnly.FromDateTime(new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc)):O}");
        }
        else if (type == typeof(DateTime))
        {
            schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc):O}");
        }
        else if (type == typeof(TimeOnly))
        {
            schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{new TimeOnly(12, 0, 0)}");
        }

        return Task.CompletedTask;
    }
}
