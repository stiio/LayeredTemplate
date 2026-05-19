using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

[EndpointGroup<TodoListsGroup>]
public sealed class GetTodoList : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:guid}", Handle)
            .WithName(nameof(GetTodoList))
            .WithSummary("Get TodoList by id");

    public static TodoListDto Handle(Guid id) =>
        new()
        {
            Id = id,
            Name = "List 1",
            Description = "List description",
            CreatedAt = DateTime.UtcNow,
        };
}
