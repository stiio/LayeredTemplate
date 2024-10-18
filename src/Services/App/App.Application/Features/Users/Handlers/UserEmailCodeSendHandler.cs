using LayeredTemplate.App.Application.Features.Users.Requests;
using MediatR;

namespace LayeredTemplate.App.Application.Features.Users.Handlers;

internal class UserEmailCodeSendHandler : IRequestHandler<UserEmailCodeSendRequest>
{
    public Task Handle(UserEmailCodeSendRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}