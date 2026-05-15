using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features.TodoLists;

public static class DeleteTodoList
{
    public static void Configure(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", Handle)
            .WithName(nameof(DeleteTodoList))
            .WithSummary("Delete TodoList");

    public static NoContent Handle(Guid id) => TypedResults.NoContent();
}
