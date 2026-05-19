using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.Users;

public sealed class UsersGroup : IEndpointGroup
{
    public static RouteGroupBuilder MapGroup(IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .WithGroupName("v1")
            .RequireAuthorization();
}
