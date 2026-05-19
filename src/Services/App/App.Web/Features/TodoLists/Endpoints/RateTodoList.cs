using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Features.TodoLists.Services;
using LayeredTemplate.App.Shared.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

/// <summary>
/// Demonstrates consumption of a feature-internal service: <see cref="ITodoListRatingService"/>
/// is registered via <c>TodoListsGroup.ConfigureServices</c> (the feature's
/// <see cref="IFeatureServices"/> implementation) and injected here as a normal handler parameter.
/// </summary>
[EndpointGroup<TodoListsGroup>]
public sealed class RateTodoList : IEndpoint
{
    public sealed record Response(decimal Rating);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/rate", Handle)
            .WithName(nameof(RateTodoList))
            .WithSummary("Compute a rating for a TodoList");

    public static Response Handle([FromBody] TodoListDto todoList, ITodoListRatingService rating) =>
        new(rating.Rate(todoList));
}
