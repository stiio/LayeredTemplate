using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Pagination;

namespace LayeredTemplate.App.Features.TodoLists;

public sealed partial class SearchTodoLists
{
    public sealed class Response
    {
        public TodoListSearchFilterDto? Filter { get; init; }

        public Sorting<TodoListFields> Sorting { get; init; } = null!;

        public PaginationResponse Pagination { get; init; } = null!;

        public TodoListDto[] Data { get; init; } = null!;
    }
}
