using Humanizer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

public class DefaultSchemaDescriptionFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Description ??= context.MemberInfo?.Name?.Humanize()
                               ?? context.ParameterInfo?.Name?.Humanize();
    }
}