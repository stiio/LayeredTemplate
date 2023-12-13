﻿using AutoBogus;
using LayeredTemplate.Application.TodoLists.Models;

namespace LayeredTemplate.Web.Bogus.TodoLists;

internal sealed class TodoListDtoFaker : AutoFaker<TodoListDto>
{
    public TodoListDtoFaker()
    {
        this.RuleFor(x => x.Name, f => f.Lorem.Word());
    }
}