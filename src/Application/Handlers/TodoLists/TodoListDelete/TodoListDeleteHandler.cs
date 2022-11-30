using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.Events.TodoLists;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists.TodoListDelete;

internal class TodoListDeleteHandler : IRequestHandler<TodoListDeleteRequest>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IResourceAuthorizationService resourceAuthorizationService;
    private readonly IPublisher publisher;

    public TodoListDeleteHandler(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService,
        IPublisher publisher)
    {
        this.dbContext = dbContext;
        this.resourceAuthorizationService = resourceAuthorizationService;
        this.publisher = publisher;
    }

    public async Task<Unit> Handle(TodoListDeleteRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.dbContext.TodoLists.FindAsync(request.Id);

        if (todoList is null)
        {
            throw new NotFoundException(nameof(TodoList), request.Id);
        }

        await this.resourceAuthorizationService.Authorize(todoList, Operations.FullAccess);

        this.dbContext.TodoLists.Remove(todoList);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        await this.publisher.Publish(new TodoListDeletedEvent(todoList.Id), cancellationToken);

        return Unit.Value;
    }
}