using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListFileCreateHandler : IRequestHandler<TodoListFileCreateRequest, TodoListDto>
{
    public ValueTask<TodoListDto> Handle(TodoListFileCreateRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<TodoListDto>(new TodoListDto()
        {
            Id = Guid.NewGuid(),
            Name = "List 1",
            Description = "List description",
            CreatedAt = DateTime.UtcNow,
        });
    }
}