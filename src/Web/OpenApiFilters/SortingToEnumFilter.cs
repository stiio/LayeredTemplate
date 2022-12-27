using System.Reflection;
using LayeredTemplate.Application.Contracts.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

public class SortingToEnumFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() == typeof(Sorting<>))
        {
            var columnSchema = schema.Properties[nameof(Sorting.Column).ToLower()];
            var recordType = context.Type.GetGenericArguments()[0];

            columnSchema.Enum = this.CreateOpenApiEnum(recordType);
        }
    }

    private IList<IOpenApiAny> CreateOpenApiEnum(Type type)
    {
        var result = new List<IOpenApiAny>();

        foreach (var property in type.GetProperties())
        {
            this.FillOpenApiEnum(property, result);
        }

        return result;
    }

    private void FillOpenApiEnum(PropertyInfo propertyInfo, List<IOpenApiAny> result, string? prefix = null, int level = 0)
    {
        if (level == 3)
        {
            return;
        }

        if (!propertyInfo.PropertyType.IsClass || propertyInfo.PropertyType == typeof(string))
        {
            result.Add(new OpenApiString(prefix is null ? propertyInfo.Name : $"{prefix}.{propertyInfo.Name}"));
            return;
        }

        foreach (var nestedProperty in propertyInfo.PropertyType.GetProperties())
        {
            this.FillOpenApiEnum(
                nestedProperty,
                result,
                prefix is null ? propertyInfo.Name : $"{prefix}.{propertyInfo.Name}",
                level++);
        }
    }
}