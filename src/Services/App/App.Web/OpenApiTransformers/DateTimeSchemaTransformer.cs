using System.Text.Json.Nodes;
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
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Example = JsonNode.Parse($"\"{DateOnly.FromDateTime(new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc)):O}\"");
        }
        else if (type == typeof(DateTime))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Example = JsonNode.Parse($"\"{new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc):O}\"");
        }
        else if (type == typeof(TimeOnly))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Example = JsonNode.Parse($"\"{new TimeOnly(12, 0, 0)}\"");
        }

        return Task.CompletedTask;
    }
}
