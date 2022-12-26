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

            columnSchema.Enum = recordProperties.Select(recordProperty => new OpenApiString(recordProperty.Name)).ToArray();
        }
    }
}