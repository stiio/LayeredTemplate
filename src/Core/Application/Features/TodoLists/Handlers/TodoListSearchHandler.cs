using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.TodoLists.Extensions;
using LayeredTemplate.Application.Features.TodoLists.Models;
using LayeredTemplate.Application.Features.TodoLists.Requests;
using LayeredTemplate.Application.Features.Users.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Features.TodoLists.Handlers;

internal class TodoListSearchHandler : IRequestHandler<TodoListSearchRequest, TodoListSearchResponse>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUserService;
    private readonly IMapper mapper;

    public TodoListSearchHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        this.context = context;
        this.currentUserService = currentUserService;
        this.mapper = mapper;
    }

    public async Task<TodoListSearchResponse> Handle(TodoListSearchRequest request, CancellationToken cancellationToken)
    {
        var query = this.context.TodoLists
            .ForUser(this.currentUserService.UserId)
            .ProjectTo<TodoListRecordDto>(this.mapper.ConfigurationProvider)
            .ApplyFilter(request.Body.Filter)
            .Sort(request.Body.Sorting);

        return new TodoListSearchResponse()
        {
            Filter = request.Body.Filter,
            Pagination = await query.ToPaginationResponse(request.Body.Pagination, cancellationToken),
            Sorting = request.Body.Sorting,
            Data = await query
                .Page(request.Body.Pagination)
                .ToArrayAsync(cancellationToken),
        };
    }
}