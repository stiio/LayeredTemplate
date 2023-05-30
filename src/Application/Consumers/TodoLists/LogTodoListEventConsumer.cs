using LayeredTemplate.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Application.Consumers.TodoLists;

public class LogTodoListEventConsumer : IConsumer<TodoListCreated>
{
    private readonly ILogger<LogTodoListEventConsumer> logger;

    public LogTodoListEventConsumer(ILogger<LogTodoListEventConsumer> logger)
    {
        this.logger = logger;
    }

    public Task Consume(ConsumeContext<TodoListCreated> context)
    {
        this.logger.LogInformation($"Todo List Created Event: {context.Message}");
        return Task.CompletedTask;
    }
}