using Humanizer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class DefaultOperationSummaryTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        if (methodInfo is not null)
        {
            operation.Summary ??= methodInfo.Name.Humanize();
        }

        return Task.CompletedTask;
    }
}
