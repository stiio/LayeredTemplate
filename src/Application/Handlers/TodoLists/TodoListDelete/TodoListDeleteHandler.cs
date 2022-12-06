using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists.TodoListDelete;

internal class TodoListDeleteHandler : IRequestHandler<TodoListDeleteRequest>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IResourceAuthorizationService resourceAuthorizationService;

    public TodoListDeleteHandler(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.dbContext = dbContext;
        this.resourceAuthorizationService = resourceAuthorizationService;
    }

    public async Task<Unit> Handle(TodoListDeleteRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.dbContext.TodoLists.FindAsync(request.Id);
        if (todoList is null)
        {
            throw new NotFoundException(nameof(TodoList), request.Id);
        }

        var authorizationResult = await this.resourceAuthorizationService.Authorize(todoList, Operations.FullAccess);
        if (!authorizationResult.Succeeded)
        {
            throw new AccessDeniedException();
        }

        this.dbContext.TodoLists.Remove(todoList);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}