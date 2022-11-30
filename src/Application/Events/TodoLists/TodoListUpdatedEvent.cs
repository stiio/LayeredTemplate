using MediatR;

namespace LayeredTemplate.Application.Events.TodoLists;

internal class TodoListUpdatedEvent : INotification
{
    public TodoListUpdatedEvent(Guid todoListId)
    {
        this.TodoListId = todoListId;
    }

    public Guid TodoListId { get; set; }
}