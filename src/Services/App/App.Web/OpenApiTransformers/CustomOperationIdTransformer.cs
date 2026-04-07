using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.OpenApiTransformers;

public class CustomOperationIdTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        if (methodInfo is not null && context.Description.GroupName is not null)
        {
            operation.OperationId = $"{methodInfo.Name}{context.Description.GroupName.ToUpper()}";
        }

        return Task.CompletedTask;
    }
}
