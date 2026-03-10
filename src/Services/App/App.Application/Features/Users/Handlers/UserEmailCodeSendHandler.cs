using LayeredTemplate.App.Application.Features.Users.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.Users.Handlers;

internal class UserEmailCodeSendHandler : IRequestHandler<UserEmailCodeSendRequest>
{
    public ValueTask<Unit> Handle(UserEmailCodeSendRequest request, CancellationToken cancellationToken)
    {
        return Unit.ValueTask;
    }
}