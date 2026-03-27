using System.Reflection;
using System.Text.Json.Nodes;
using LayeredTemplate.App.Application.Common.Models;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

public class SortingToEnumFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() == typeof(Sorting<>))
        {
            var targetSchema = schema as OpenApiSchema;
            var columnSchema = targetSchema!.Properties![nameof(Sorting.Column).ToLower()] as OpenApiSchema;
            var recordType = context.Type.GetGenericArguments()[0];

            columnSchema!.Enum = this.CreateOpenApiEnum(recordType);
        }
    }

    private IList<JsonNode> CreateOpenApiEnum(Type type)
    {
        var result = new List<JsonNode>();

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

        result.Add(JsonNode.Parse(enumValue)!);
    }
}