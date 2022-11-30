using MediatR;

namespace LayeredTemplate.Application.Events.TodoLists;

internal class TodoListCreatedEvent : INotification
{
    public TodoListCreatedEvent(Guid todoListId)
    {
        this.TodoListId = todoListId;
    }

    public Guid TodoListId { get; set; }
}