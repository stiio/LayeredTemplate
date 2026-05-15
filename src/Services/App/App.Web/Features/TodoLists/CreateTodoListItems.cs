using LayeredTemplate.App.Features.TodoLists.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

public static class CreateTodoListItems
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/items", Handle)
            .WithName(nameof(CreateTodoListItems))
            .WithSummary("Create TodoList items (polymorphic example)");

    public static TodoListItemBase[] Handle([FromBody] TodoListItemBase[] items) => items;
}
