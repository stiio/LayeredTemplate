using LayeredTemplate.App.Features.TodoLists.Models;

namespace LayeredTemplate.App.Features.TodoLists;

public static class GetTodoList
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", Handle)
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
