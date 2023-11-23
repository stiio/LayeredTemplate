using LayeredTemplate.Web.Api.Models;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

/// <inheritdoc />
public class DefaultApplicationResponsesFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var badResponseType = new OpenApiMediaType()
        {
            Schema = context.SchemaGenerator.GenerateSchema(typeof(ErrorResult), context.SchemaRepository),
        };

        operation.Responses.Add(
            "400",
            new OpenApiResponse()
            {
                Description = "Bad request",
                Content = new Dictionary<string, OpenApiMediaType>()
                {
                    { "application/json", badResponseType },
                },
            });

        operation.Responses.Add(
            "500",
            new OpenApiResponse()
            {
                Description = "Internal server error",
                Content = new Dictionary<string, OpenApiMediaType>()
                {
                    { "application/json", badResponseType },
                },
            });
    }
}