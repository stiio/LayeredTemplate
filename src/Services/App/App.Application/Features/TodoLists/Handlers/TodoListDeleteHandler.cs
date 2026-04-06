using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListDeleteHandler : IRequestHandler<TodoListDeleteRequest>
{
    public ValueTask<Unit> Handle(TodoListDeleteRequest request, CancellationToken cancellationToken)
    {
        return Unit.ValueTask;
    }
}