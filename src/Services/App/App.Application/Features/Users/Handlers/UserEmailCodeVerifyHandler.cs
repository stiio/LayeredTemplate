using LayeredTemplate.App.Application.Features.Users.Requests;
using MediatR;

namespace LayeredTemplate.App.Application.Features.Users.Handlers;

internal class UserEmailCodeVerifyHandler : IRequestHandler<UserEmailCodeVerifyRequest>
{
    public Task Handle(UserEmailCodeVerifyRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}