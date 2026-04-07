using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LayeredTemplate.App.Application.Common.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class SortingToEnumTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Sorting<>))
        {
            if (schema.Properties?[nameof(Sorting.Column).ToLower()] is OpenApiSchema columnSchema)
            {
                var recordType = type.GetGenericArguments()[0];
                columnSchema.Enum = this.CreateOpenApiEnum(recordType);
            }
        }

        return Task.CompletedTask;
    }

    private IList<JsonNode> CreateOpenApiEnum(Type type)
    {
        var result = new List<JsonNode>();

        if (type.IsEnum)
        {
            var values = Enum.GetNames(type);
            result.AddRange(values.Select(value => JsonSerializer.SerializeToNode(value)!));
            return result;
        }

        foreach (var property in type.GetProperties())
        {
            this.FillOpenApiEnum(property, result);
        }

        return result;
    }

    private void FillOpenApiEnum(PropertyInfo propertyInfo, List<JsonNode> result, string? prefix = null, int level = 0)
    {
        if (level == 3)
        {
            return;
        }

        if (propertyInfo.PropertyType.IsArray)
        {
            return;
        }

        if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
        {
            foreach (var nestedProperty in propertyInfo.PropertyType.GetProperties())
            {
                this.FillOpenApiEnum(
                    nestedProperty,
                    result,
                    prefix is null ? propertyInfo.Name : $"{prefix}.{propertyInfo.Name}",
                    level++);
            }

            return;
        }

        var enumValue = prefix is null ? propertyInfo.Name : $"{prefix}.{propertyInfo.Name}";
        if (result.Any(x => x.ToString().Replace(".", string.Empty) == enumValue.Replace(".", string.Empty)))
        {
            return;
        }

        result.Add(JsonSerializer.SerializeToNode(enumValue)!);
    }
}
