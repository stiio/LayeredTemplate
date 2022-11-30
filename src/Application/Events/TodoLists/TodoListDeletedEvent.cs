using MediatR;

namespace LayeredTemplate.Application.Events.TodoLists;

internal class TodoListDeletedEvent : INotification
{
    public TodoListDeletedEvent(Guid todoListId)
    {
        this.TodoListId = todoListId;
    }

    public Guid TodoListId { get; set; }
}