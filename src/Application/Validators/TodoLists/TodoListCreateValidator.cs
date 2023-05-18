﻿using FluentValidation;
using LayeredTemplate.Application.Contracts.Requests.TodoLists;

namespace LayeredTemplate.Application.Validators.TodoLists;

internal class TodoListCreateValidator : AbstractValidator<TodoListCreateRequest>
{
    public TodoListCreateValidator()
    {
    }
}