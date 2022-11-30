using LayeredTemplate.Application.Events.TodoLists;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.EventHandlers.TodoLists;

internal class TodoListDeletedEventHandler : INotificationHandler<TodoListDeletedEvent>
{
    private readonly ILogger logger;

    public TodoListDeletedEventHandler(ILogger<TodoListDeletedEventHandler> logger)
    {
        this.logger = logger;
    }

    public Task Handle(TodoListDeletedEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("TodoList '{todoListId}' deleted.", notification.TodoListId);

        return Task.CompletedTask;
    }
}