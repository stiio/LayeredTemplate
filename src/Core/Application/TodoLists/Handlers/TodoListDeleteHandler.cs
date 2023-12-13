using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.TodoLists.Requests;
using MediatR;

namespace LayeredTemplate.Application.TodoLists.Handlers;

internal class TodoListDeleteHandler : IRequestHandler<TodoListDeleteRequest>
{
    private readonly IApplicationDbContext context;

    public TodoListDeleteHandler(IApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task Handle(TodoListDeleteRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.context.TodoLists.FirstById(request.Id, cancellationToken);
        this.context.TodoLists.Remove(todoList);
        await this.context.SaveChangesAsync(cancellationToken);
    }
}