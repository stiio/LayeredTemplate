using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Messaging.Contracts;
using MassTransit;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListCreateHandler : IRequestHandler<TodoListCreateRequest, TodoListDto>
{
    private readonly ICurrentUserService currentUserService;
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;
    private readonly IPublishEndpoint publishEndpoint;

    public TodoListCreateHandler(
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        IMapper mapper,
        IPublishEndpoint publishEndpoint)
    {
        this.currentUserService = currentUserService;
        this.context = context;
        this.mapper = mapper;
        this.publishEndpoint = publishEndpoint;
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

        await this.publishEndpoint.Publish(new TodoListCreated(todoList.Id, todoList.Name), cancellationToken);

        return await this.context.TodoLists
            .ProjectTo<TodoListDto>(this.mapper.ConfigurationProvider)
            .FirstById(todoList.Id, cancellationToken);
    }
}