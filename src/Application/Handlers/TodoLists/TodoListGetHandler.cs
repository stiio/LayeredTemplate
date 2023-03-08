using AutoMapper;
using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;
using MediatR;

namespace LayeredTemplate.Application.Handlers.TodoLists;

internal class TodoListGetHandler : IRequestHandler<TodoListGetRequest, TodoListDto>
{
    private readonly IApplicationDbContext dbsContext;
    private readonly IResourceAuthorizationService resourceAuthorizationService;
    private readonly IMapper mapper;

    public TodoListGetHandler(
        IApplicationDbContext dbsContext,
        IResourceAuthorizationService resourceAuthorizationService,
        IMapper mapper)
    {
        this.dbsContext = dbsContext;
        this.resourceAuthorizationService = resourceAuthorizationService;
        this.mapper = mapper;
    }

    public async Task<TodoListDto> Handle(TodoListGetRequest request, CancellationToken cancellationToken)
    {
        var todoList = await this.dbsContext.TodoLists.FindAsync(request.Id);
        if (todoList is null)
        {
            throw new AppNotFoundException(nameof(TodoList), request.Id);
        }

        var authorizationResult = await this.resourceAuthorizationService.Authorize(todoList, Operations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new AccessDeniedException();
        }

        return this.mapper.Map<TodoListDto>(todoList);
    }
}