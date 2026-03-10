using LayeredTemplate.App.Application.Features.Users.Models;
using LayeredTemplate.App.Application.Features.Users.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.Users.Handlers;

internal class CurrentUserGetHandler : IRequestHandler<CurrentUserGetRequest, CurrentUser>
{
    public ValueTask<CurrentUser> Handle(CurrentUserGetRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new CurrentUser()
        {
            Id = new Guid("53803690-346B-4BBE-AA6A-28C0CF568831"),
            Email = "example@email.com",
            EmailVerified = true,
            FirstName = "John",
            LastName = "Doe",
            Phone = "+12106542673",
            PhoneVerified = true,
        });
    }
}