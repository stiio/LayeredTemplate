﻿using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Validators.TodoLists;

internal class TodoListUpdateRequestValidator : AbstractValidator<TodoListUpdateRequest>
{
    public TodoListUpdateRequestValidator(
        IApplicationDbContext context,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<TodoListUpdateRequest, Guid, TodoList>(context);

        this.RuleFor(x => x.Id)
            .RequireAccess<TodoListUpdateRequest, Guid, TodoList>(Operations.Update, context, resourceAuthorizationService);
    }
}