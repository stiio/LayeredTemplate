using LayeredTemplate.App.Shared.Errors;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Adds default 400/500 ProblemDetails responses to every operation, so generated clients
/// have a typed error path without each endpoint having to declare it.
/// </summary>
internal sealed class DefaultApplicationResponsesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var errorSchemaRef = new OpenApiSchemaReference(nameof(AppProblemDetails));
        var badResponseContent = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new() { Schema = errorSchemaRef },
        };

        operation.Responses ??= [];

        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse { Description = "Bad request", Content = badResponseContent });
        }

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse { Description = "Internal server error", Content = badResponseContent });
        }

        return Task.CompletedTask;
    }
}
