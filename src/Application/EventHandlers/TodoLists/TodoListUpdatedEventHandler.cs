using LayeredTemplate.Application.Events.TodoLists;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.EventHandlers.TodoLists;

internal class TodoListUpdatedEventHandler : INotificationHandler<TodoListUpdatedEvent>
{
    private readonly ILogger logger;

    public TodoListUpdatedEventHandler(ILogger<TodoListUpdatedEventHandler> logger)
    {
        this.logger = logger;
    }

    public Task Handle(TodoListUpdatedEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("TodoList '{todoListId}' updated.", notification.TodoListId);

        return Task.CompletedTask;
    }
}