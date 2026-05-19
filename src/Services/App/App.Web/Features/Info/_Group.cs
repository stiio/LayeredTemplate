using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.Info;

public sealed class InfoGroup : IEndpointGroup
{
    public static RouteGroupBuilder MapGroup(IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/info")
            .WithTags("Info")
            .WithGroupName("v1");
}
