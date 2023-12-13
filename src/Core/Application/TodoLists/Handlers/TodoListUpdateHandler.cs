using AutoMapper;
using AutoMapper.QueryableExtensions;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.TodoLists.Models;
using LayeredTemplate.Application.TodoLists.Requests;
using LayeredTemplate.Domain.Entities;
using MediatR;

namespace LayeredTemplate.Application.TodoLists.Handlers;

internal class TodoListUpdateHandler : IRequestHandler<TodoListUpdateRequest, TodoListDto>
{
    private readonly IApplicationDbContext context;
    private readonly IMapper mapper;
    private readonly ILockProvider lockProvider;

    public TodoListUpdateHandler(
        IApplicationDbContext context,
        IMapper mapper,
        ILockProvider lockProvider)
    {
        this.context = context;
        this.mapper = mapper;
        this.lockProvider = lockProvider;
    }

    public async Task<TodoListDto> Handle(TodoListUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var @lock = await this.lockProvider.AcquireLockAsync($"todoList:{request.Id}", cancellationToken: cancellationToken);
        await using var transaction = await this.context.BeginTransactionAsync(cancellationToken);

        var todoList = await this.context.TodoLists.FirstById(request.Id, cancellationToken);
        if (todoList is null)
        {
            throw new AppNotFoundException(nameof(TodoList), request.Id);
        }

        this.mapper.Map(request.Body, todoList);

        await this.context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await @lock.DisposeAsync();

        return await this.context.TodoLists
            .ProjectTo<TodoListDto>(this.mapper.ConfigurationProvider)
            .FirstById(request.Id, cancellationToken);
    }
}