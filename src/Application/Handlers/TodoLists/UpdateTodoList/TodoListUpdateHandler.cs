﻿using AutoMapper;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists.UpdateTodoList;

internal class TodoListUpdateHandler : IRequestHandler<TodoListUpdateRequest, TodoListDto>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IResourceAuthorizationService resourceAuthorizationService;
    private readonly IMapper mapper;

    public TodoListUpdateHandler(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.resourceAuthorizationService = resourceAuthorizationService;
        this.mapper = mapper;
    }

    public async Task<TodoListDto> Handle(TodoListUpdateRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.dbContext.TodoLists.FindAsync(request.Id);

        if (todoList is null)
        {
            throw new NotFoundException(nameof(TodoList), request.Id);
        }

        await this.resourceAuthorizationService.Authorize(todoList, Operations.FullAccess);

        this.mapper.Map(request, todoList);

        this.dbContext.TodoLists.Update(todoList);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        return this.mapper.Map<TodoListDto>(todoList);
    }
}