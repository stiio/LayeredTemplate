using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListUpdateHandler : IRequestHandler<TodoListUpdateRequest, TodoListDto>
{
    public ValueTask<TodoListDto> Handle(TodoListUpdateRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<TodoListDto>(new TodoListDto()
        {
            Id = request.Id,
            Name = request.Body.Name,
            Description = request.Body.Description,
            CreatedAt = DateTime.UtcNow,
        });
    }
}