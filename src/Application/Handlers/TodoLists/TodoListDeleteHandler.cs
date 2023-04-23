using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListDeleteHandler : IRequestHandler<TodoListDeleteRequest>
{
    private readonly IApplicationDbContext context;

    public TodoListDeleteHandler(IApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task Handle(TodoListDeleteRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.context.TodoLists.FindById(request.Id, cancellationToken);
        this.context.TodoLists.Remove(todoList);
        await this.context.SaveChangesAsync(cancellationToken);
    }
}