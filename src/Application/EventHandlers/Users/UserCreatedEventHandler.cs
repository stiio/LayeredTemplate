using LayeredTemplate.Application.Events.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.EventHandlers.Users;

internal class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedEventHandler> logger;

    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    {
        this.logger = logger;
    }

    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("User '{userId}' created.", notification.UserId);

        return Task.CompletedTask;
    }
}