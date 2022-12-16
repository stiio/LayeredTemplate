using AutoMapper;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListCreateHandler : IRequestHandler<TodoListCreateRequest, TodoListDto>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IApplicationDbContext dbsContext;
    private readonly IMapper mapper;

    public TodoListCreateHandler(
        ICurrentUserService currentUserService,
        IApplicationDbContext dbsContext,
        IMapper mapper)
    {
        this.currentUserService = currentUserService;
        this.dbsContext = dbsContext;
        this.mapper = mapper;
    }

    public async Task<TodoListDto> Handle(TodoListCreateRequest request, CancellationToken cancellationToken)
    {
        var todoList = new TodoList()
        {
            UserId = this.currentUserService.UserId,
            Name = request.Name,
            Type = request.Type,
        };

        await this.dbsContext.TodoLists.AddAsync(todoList, cancellationToken);
        await this.dbsContext.SaveChangesAsync(cancellationToken);

        return this.mapper.Map<TodoListDto>(todoList);
    }
}