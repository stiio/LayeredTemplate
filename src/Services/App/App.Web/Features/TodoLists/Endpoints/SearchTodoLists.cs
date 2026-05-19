using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;
using LayeredTemplate.App.Shared.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

/// <summary>
/// Demonstrates the "endpoint split into partial files" convention used when an endpoint outgrows
/// a single file (~150+ lines, ≥3 nested types). The outer class is marked <c>partial</c>; nested
/// types live in sibling files named <c>SearchTodoLists.&lt;Part&gt;.cs</c>
/// (see <see cref="Request"/>, <see cref="Response"/>). Map + Handle stay here as the
/// endpoint's "entry surface".
/// </summary>
/// <remarks>
/// Why partial: keeps types <i>nested</i> in <c>SearchTodoLists</c>, so OpenAPI schema names stay
/// <c>SearchTodoListsRequest</c> / <c>SearchTodoListsResponse</c> (via the parent-name-prepend rule
/// in <c>ConfigureOpenApi.CreateSchemaReferenceId</c>). A flat split into separate top-level
/// classes would lose that auto-naming and require manual avoidance of clashes.
/// </remarks>
[EndpointGroup<TodoListsGroup>]
public sealed partial class SearchTodoLists : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/search", Handle)
            .WithName(nameof(SearchTodoLists))
            .WithSummary("Search TodoLists");

    public static Response Handle([FromBody] Request request) =>
        new()
        {
            Pagination = new PaginationResponse
            {
                Page = request.Pagination.Page,
                Limit = request.Pagination.Limit,
                Total = 3,
            },
            Filter = request.Filter,
            Sorting = request.Sorting,
            Data =
            [
                new TodoListDto { Id = Guid.NewGuid(), Name = "List 1", Description = "List description 1", CreatedAt = DateTime.UtcNow },
                new TodoListDto { Id = Guid.NewGuid(), Name = "List 2", Description = "List description 2", CreatedAt = DateTime.UtcNow },
                new TodoListDto { Id = Guid.NewGuid(), Name = "List 3", Description = "List description 3", CreatedAt = DateTime.UtcNow },
            ],
        };
}
