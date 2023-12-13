using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.TodoLists.Models;
using LayeredTemplate.Application.Features.TodoLists.Requests;
using MediatR;

namespace LayeredTemplate.Application.Features.TodoLists.Handlers;

internal class TodoListGetHandler : IRequestHandler<TodoListGetRequest, TodoListDto>
{
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;

    public TodoListGetHandler(
        IApplicationDbContext context,
        IMapper mapper)
    {
        this.context = context;
        this.mapper = mapper;
    }

    public Task<TodoListDto> Handle(TodoListGetRequest request, CancellationToken cancellationToken)
    {
        return this.context.TodoLists
            .ProjectTo<TodoListDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}