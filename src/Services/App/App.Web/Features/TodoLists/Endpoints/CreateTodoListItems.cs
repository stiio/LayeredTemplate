using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

[EndpointGroup<TodoListsGroup>]
public sealed class CreateTodoListItems : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/items", Handle)
            .WithName(nameof(CreateTodoListItems))
            .WithSummary("Create TodoList items (polymorphic example)");

    public static TodoListItemBase[] Handle([FromBody] TodoListItemBase[] items) => items;
}
