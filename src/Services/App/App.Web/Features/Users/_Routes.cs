using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.Users;

public sealed class UsersRoutes : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .WithGroupName("v1")
            .RequireAuthorization();

        GetCurrentUser.Configure(v1);
        SendUserEmailCode.Configure(v1);
        VerifyUserEmailCode.Configure(v1);
    }
}
