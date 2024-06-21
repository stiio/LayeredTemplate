using LayeredTemplate.Application.Features.Users.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Handlers;

internal class UserEmailCodeSendHandler : IRequestHandler<UserEmailCodeSendRequest>
{
    public Task Handle(UserEmailCodeSendRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}