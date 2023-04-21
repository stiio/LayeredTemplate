using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Validators.TodoLists;

internal class TodoListUpdateValidator : AbstractValidator<TodoListUpdateRequest>
{
    public TodoListUpdateValidator(
        IApplicationDbContext context,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .RequireAccess<TodoListUpdateRequest, Guid, TodoList>(Operations.Update, context, resourceAuthorizationService);
    }
}