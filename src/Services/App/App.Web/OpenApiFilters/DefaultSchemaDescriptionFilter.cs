using Humanizer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

public class DefaultSchemaDescriptionFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Description ??= context.MemberInfo?.Name?.Humanize()
                               ?? context.ParameterInfo?.Name?.Humanize()
                               ?? (context.Type.IsGenericType ? null : context.Type.Name.Humanize());
    }
}