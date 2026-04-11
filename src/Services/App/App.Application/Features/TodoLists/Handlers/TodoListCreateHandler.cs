using LayeredTemplate.App.Application.Common.Exceptions;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListCreateHandler : IRequestHandler<TodoListCreateRequest, TodoListDto>
{
    public ValueTask<TodoListDto> Handle(TodoListCreateRequest request, CancellationToken cancellationToken)
    {
        throw new HttpStatusException("Some message");
        return new ValueTask<TodoListDto>(new TodoListDto()
        {
            Id = Guid.NewGuid(),
            Name = "List 1",
            Description = "List description",
            CreatedAt = DateTime.UtcNow,
        });
    }
}