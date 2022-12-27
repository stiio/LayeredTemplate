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
            var recordProperties = context.Type.GetGenericArguments()[0].GetProperties();

            columnSchema.Enum = recordProperties.SelectMany(recordProperty => this.CreateOpenApiEnum(recordProperty)).ToArray();
        }
    }

    private IList<IOpenApiAny> CreateOpenApiEnum(
        PropertyInfo propertyInfo,
        IList<IOpenApiAny>? result = null,
        string? prefix = null,
        int level = 0)
    {
        if (level == 3)
        {
            return result!;
        }

        result ??= new List<IOpenApiAny>();

        if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
        {
            foreach (var nestedProperty in propertyInfo.PropertyType.GetProperties())
            {
                this.CreateOpenApiEnum(
                    nestedProperty,
                    result,
                    string.Join(".", new[] { prefix, propertyInfo.Name }.Where(x => x != null)),
                    level++);
            }

            return result;
        }

        result.Add(new OpenApiString(string.IsNullOrEmpty(prefix) ? propertyInfo.Name : $"{prefix}.{propertyInfo.Name}"));

        return result;
    }
}