using LayeredTemplate.App.Features.Users.Models;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.Users.Endpoints;

[EndpointGroup<UsersGroup>]
public sealed class GetCurrentUser : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/current_user", Handle)
            .WithName(nameof(GetCurrentUser))
            .WithSummary("Get current user");

    public static CurrentUserDto Handle() =>
        new()
        {
            // TODO: read from ICurrentUser / DB once feature is implemented. Stub preserved from
            // the previous handler so demo-mode HTTP calls keep returning a body.
            Id = new Guid("53803690-346B-4BBE-AA6A-28C0CF568831"),
            Email = "example@email.com",
            EmailVerified = true,
            FirstName = "John",
            LastName = "Doe",
            Phone = "+12106542673",
            PhoneVerified = true,
        };
}
