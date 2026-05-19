using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features._Dev;

/// <summary>
/// Development-only route group rooted at <c>/api/dev/</c>. <see cref="DevOnlyAttribute"/> on the
/// group makes discovery skip group materialization outside Development; endpoints targeting this
/// group (which themselves carry <see cref="DevOnlyAttribute"/>) cascade-skip too — the routes
/// don't exist on the route table in staging/production at all.
/// </summary>
[DevOnly]
public sealed class DevGroup : IEndpointGroup
{
    public static RouteGroupBuilder MapGroup(IEndpointRouteBuilder app) =>
        app.MapGroup("/api/dev")
            .WithTags("Development")
            .WithGroupName("dev");
}
