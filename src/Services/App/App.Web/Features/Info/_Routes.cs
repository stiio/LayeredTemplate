using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.Info;

/// <summary>Build/version information endpoint. Single-endpoint feature.</summary>
public sealed class InfoRoutes : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/info").WithTags("Info").WithGroupName("v1");
        GetInfo.Configure(v1);
    }
}
