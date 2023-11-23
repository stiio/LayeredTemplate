using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;
using LayeredTemplate.Application.QueryableExtensions;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

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