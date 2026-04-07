using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.App.Application.Features.TodoLists.Requests;
using Mediator;

namespace LayeredTemplate.App.Application.Features.TodoLists.Handlers;

internal class TodoListItemsCreateHandler : IRequestHandler<TodoListItemsCreateRequest, TodoListItemBase[]>
{
    public ValueTask<TodoListItemBase[]> Handle(TodoListItemsCreateRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(request.Body);
    }
}