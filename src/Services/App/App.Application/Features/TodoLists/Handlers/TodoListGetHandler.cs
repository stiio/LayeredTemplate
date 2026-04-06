using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListGetHandler : IRequestHandler<TodoListGetRequest, TodoListDto>
{
    public ValueTask<TodoListDto> Handle(TodoListGetRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<TodoListDto>(new TodoListDto()
        {
            Id = request.Id,
            Name = "List 1",
            Description = "List description",
            CreatedAt = DateTime.UtcNow,
        });
    }
}