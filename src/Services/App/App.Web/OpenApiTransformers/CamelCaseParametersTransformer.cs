using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class CamelCaseParametersTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        for (var i = 0; i < operation.Parameters.Count; i++)
        {
            if (operation.Parameters[i] is OpenApiParameter parameter)
            {
                parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
            }
        }

        return Task.CompletedTask;
    }
}
