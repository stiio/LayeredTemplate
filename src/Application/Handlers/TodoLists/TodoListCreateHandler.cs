using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListCreateHandler : IRequestHandler<TodoListCreateRequest, TodoListDto>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;

    public TodoListCreateHandler(
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        IMapper mapper)
    {
        this.currentUserService = currentUserService;
        this.context = context;
        this.mapper = mapper;
    }

    public async Task<TodoListDto> Handle(TodoListCreateRequest request, CancellationToken cancellationToken)
    {
        var todoList = new TodoList()
        {
            UserId = this.currentUserService.UserId,
            Name = request.Body.Name,
            Type = request.Body.Type,
        };

        await this.context.TodoLists.AddAsync(todoList, cancellationToken);
        await this.context.SaveChangesAsync(cancellationToken);

        return await this.context.TodoLists
            .ProjectTo<TodoListDto>(this.mapper.ConfigurationProvider)
            .FindById(todoList.Id, cancellationToken);
    }
}