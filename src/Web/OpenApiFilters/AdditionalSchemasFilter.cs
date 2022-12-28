using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

public class AdditionalSchemasFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Example
        context.SchemaGenerator.GenerateSchema(typeof(DayOfWeek), context.SchemaRepository);
    }
}