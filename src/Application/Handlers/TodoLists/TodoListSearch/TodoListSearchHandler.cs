using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Common;
using LayeredTemplate.Application.Contracts.Enums;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists.TodoListSearch;

internal class TodoListSearchHandler : IRequestHandler<TodoListSearchRequest, PagedList<TodoListRecordDto>>
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

    public Task<PagedList<TodoListRecordDto>> Handle(TodoListSearchRequest request, CancellationToken cancellationToken)
    {
        request.Sorting ??= new Sorting()
        {
            Column = nameof(TodoListRecordDto.CreatedAt),
            Direction = DirectionType.Desc,
        };

        return this.dbsContext.TodoLists
            .ForUser(this.currentUserService.UserId)
            .OrderByDescending(todoList => todoList.CreatedAt)
            .MapTodoListRecordDto()
            .ApplyFilter(request.Filter)
            .ApplySorting(request.Sorting)
            .ToPagedList(request.Pagination, cancellationToken);
    }
}