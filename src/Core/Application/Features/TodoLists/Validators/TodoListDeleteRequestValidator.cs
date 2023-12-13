using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.TodoLists.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Features.TodoLists.Validators;

internal class TodoListDeleteRequestValidator : AbstractValidator<TodoListDeleteRequest>
{
    public TodoListDeleteRequestValidator(
        IApplicationDbContext context,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<TodoListDeleteRequest, Guid, TodoList>(context);

        this.RuleFor(x => x.Id)
            .RequireAccess<TodoListDeleteRequest, Guid, TodoList>(Operations.Delete, context, resourceAuthorizationService);
    }
}