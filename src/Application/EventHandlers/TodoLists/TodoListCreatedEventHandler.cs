using LayeredTemplate.Application.Events.TodoLists;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.EventHandlers.TodoLists;

internal class TodoListCreatedEventHandler : INotificationHandler<TodoListCreatedEvent>
{
    private readonly ILogger logger;

    public TodoListCreatedEventHandler(ILogger<TodoListCreatedEventHandler> logger)
    {
        this.logger = logger;
    }

    public Task Handle(TodoListCreatedEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("TodoList '{todoListId}' created.", notification.TodoListId);

        return Task.CompletedTask;
    }
}