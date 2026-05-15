using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

public static class SearchTodoLists
{
    public sealed class Request
    {
        public TodoListSearchFilterDto? Filter { get; set; }

        public Sorting<TodoListFields> Sorting { get; set; } = new()
        {
            Column = TodoListFields.CreatedAt,
            Direction = DirectionType.Desc,
        };

        public PaginationRequest Pagination { get; set; } = new();
    }

    public sealed class Response
    {
        public TodoListSearchFilterDto? Filter { get; init; }

        public Sorting<TodoListFields> Sorting { get; init; } = null!;

        public PaginationResponse Pagination { get; init; } = null!;

        public TodoListDto[] Data { get; init; } = null!;
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/search", Handle)
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
