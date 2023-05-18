﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Application.Handlers.TodoLists;

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