using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace LayeredTemplate.Web.Conventions;

/// <inheritdoc />
public class ApiExplorerGroupPerVersionConvention : IControllerModelConvention
{
    /// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        var controllerNamespace = controller.ControllerType.Namespace; // e.g. "Controllers.V1"
        var apiVersion = controllerNamespace?.Split('.').Last().ToLower();

        apiVersion = apiVersion == "controllers" ? "development" : apiVersion;

        controller.ApiExplorer.GroupName ??= apiVersion;
    }
}