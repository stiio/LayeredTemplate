using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Validators.TodoLists;

internal class TodoListGetValidator : AbstractValidator<TodoListGetRequest>
{
    public TodoListGetValidator(
        IApplicationDbContext context,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<TodoListGetRequest, Guid, TodoList>(context);

        this.RuleFor(x => x.Id)
            .RequireAccess<TodoListGetRequest, Guid, TodoList>(Operations.Read, context, resourceAuthorizationService);
    }
}