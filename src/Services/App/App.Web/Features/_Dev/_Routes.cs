using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features._Dev;

/// <summary>
/// Development-only endpoints rooted at <c>/api/dev/</c>. <see cref="DevOnlyAttribute"/> makes
/// the endpoint discovery skip the whole class outside of Development — the routes don't exist
/// on the route table in staging/production at all.
/// </summary>
[DevOnly]
public sealed class DevRoutes : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/api/dev")
            .WithTags("Development")
            .WithGroupName("dev");

        DebugTest.Configure(dev);
    }
}
