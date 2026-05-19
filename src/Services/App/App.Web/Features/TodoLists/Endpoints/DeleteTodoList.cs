using LayeredTemplate.App.Shared.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LayeredTemplate.App.Features.TodoLists;

[EndpointGroup<TodoListsGroup>]
public sealed class DeleteTodoList : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/{id:guid}", Handle)
            .WithName(nameof(DeleteTodoList))
            .WithSummary("Delete TodoList");

    public static NoContent Handle(Guid id) => TypedResults.NoContent();
}
