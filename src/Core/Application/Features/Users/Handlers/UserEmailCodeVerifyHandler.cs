using LayeredTemplate.Application.Features.Users.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Handlers;

internal class UserEmailCodeVerifyHandler : IRequestHandler<UserEmailCodeVerifyRequest>
{
    public Task Handle(UserEmailCodeVerifyRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}