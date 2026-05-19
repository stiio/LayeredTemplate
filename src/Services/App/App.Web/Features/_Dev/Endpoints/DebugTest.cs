using LayeredTemplate.App.Shared.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features._Dev.Endpoints;

[DevOnly]
[EndpointGroup<DevGroup>]
public sealed class DebugTest : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/debug/test", Handle)
            .WithName(nameof(DebugTest))
            .WithSummary("Dev: test endpoint");

    public static Ok Handle() => TypedResults.Ok();
}
