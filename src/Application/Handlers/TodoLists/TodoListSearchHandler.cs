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
            .ApplyFilter(request.Filter)
            .Sort(request.Sorting);

        return new TodoListSearchResponse()
        {
            Filter = request.Filter,
            Pagination = await query.ToPaginationResponse(request.Pagination, cancellationToken),
            Sorting = request.Sorting,
            Data = await query
                .Page(request.Pagination, cancellationToken)
                .ToArrayAsync(cancellationToken),
        };
    }
}