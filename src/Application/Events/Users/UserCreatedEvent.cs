using MediatR;

namespace LayeredTemplate.Application.Events.Users;

internal class UserCreatedEvent : INotification
{
    public UserCreatedEvent(Guid userId)
    {
        this.UserId = userId;
    }

    public Guid UserId { get; set; }
}