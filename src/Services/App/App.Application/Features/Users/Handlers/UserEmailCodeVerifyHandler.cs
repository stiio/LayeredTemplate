using LayeredTemplate.App.Application.Features.Users.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.Users.Handlers;

internal class UserEmailCodeVerifyHandler : IRequestHandler<UserEmailCodeVerifyRequest>
{
    public ValueTask<Unit> Handle(UserEmailCodeVerifyRequest request, CancellationToken cancellationToken)
    {
        return Unit.ValueTask;
    }
}