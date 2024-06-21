using LayeredTemplate.Application.Features.Users.Models;
using LayeredTemplate.Application.Features.Users.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Handlers;

internal class CurrentUserGetHandler : IRequestHandler<CurrentUserGetRequest, CurrentUser>
{
    public Task<CurrentUser> Handle(CurrentUserGetRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CurrentUser()
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