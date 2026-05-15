using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features._Dev;

public static class DebugTest
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/debug/test", Handle)
            .WithName(nameof(DebugTest))
            .WithSummary("Dev: test endpoint");

    public static Ok Handle() => TypedResults.Ok();
}
