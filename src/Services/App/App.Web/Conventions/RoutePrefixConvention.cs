using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace LayeredTemplate.App.Web.Conventions;

public class RoutePrefixConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        var routePrefix = new AttributeRouteModel(new RouteAttribute("api/v{version:apiVersion}"));
        foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel != null
                ? AttributeRouteModel.CombineAttributeRouteModel(routePrefix, selector.AttributeRouteModel)
                : routePrefix;
        }
    }
}