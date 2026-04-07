using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class DateTimeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(DateOnly))
        {
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{DateOnly.FromDateTime(new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc)):O}");
        }
        else if (type == typeof(DateTime))
        {
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc):O}");
        }
        else if (type == typeof(TimeOnly))
        {
            schema.Format = null;
            schema.Example = JsonSerializer.SerializeToNode($"{new TimeOnly(12, 0, 0)}");
        }

        return Task.CompletedTask;
    }
}
