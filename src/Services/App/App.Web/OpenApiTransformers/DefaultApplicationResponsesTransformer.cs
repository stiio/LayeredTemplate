using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class DefaultApplicationResponsesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var errorSchemaRef = new OpenApiSchemaReference(nameof(ErrorResult));

        var badResponseType = new OpenApiMediaType()
        {
            Schema = errorSchemaRef,
        };

        operation.Responses ??= [];
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

        return Task.CompletedTask;
    }
}
