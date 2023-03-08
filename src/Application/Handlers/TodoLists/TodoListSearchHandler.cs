using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListSearchHandler : IRequestHandler<TodoListSearchRequest, TodoListSearchResponse>
{
    private readonly IApplicationDbContext dbsContext;
    private readonly ICurrentUserService currentUserService;

    public TodoListSearchHandler(
        IApplicationDbContext dbsContext,
        ICurrentUserService currentUserService)
    {
        this.dbsContext = dbsContext;
        this.currentUserService = currentUserService;
    }

    public async Task<TodoListSearchResponse> Handle(TodoListSearchRequest request, CancellationToken cancellationToken)
    {
        var query = this.dbsContext.TodoLists
            .ForUser(this.currentUserService.UserId)
            .MapTodoListRecordDto()
            .ApplyFilter(request.Body.Filter)
            .Sort(request.Body.Sorting);

        return new TodoListSearchResponse()
        {
            Filter = request.Body.Filter,
            Pagination = await query.ToPaginationResponse(request.Body.Pagination, cancellationToken),
            Sorting = request.Body.Sorting,
            Data = await query
                .Page(request.Body.Pagination, cancellationToken)
                .ToArrayAsync(cancellationToken),
        };
    }
}