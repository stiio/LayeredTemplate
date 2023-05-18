using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Application.QueryableExtensions;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListUpdateHandler : IRequestHandler<TodoListUpdateRequest, TodoListDto>
{
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;

    public TodoListUpdateHandler(
        IApplicationDbContext context,
        IMapper mapper)
    {
        this.context = context;
        this.mapper = mapper;
    }

    public async Task<TodoListDto> Handle(TodoListUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await this.context.BeginTransactionAsync(cancellationToken);

        var todoList = await this.context.TodoLists.SelectForUpdate(request.Id);
        if (todoList is null)
        {
            throw new AppNotFoundException(nameof(TodoList), request.Id);
        }

        this.mapper.Map(request.Body, todoList);

        await this.context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await this.context.TodoLists
            .ProjectTo<TodoListDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}