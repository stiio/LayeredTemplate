using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Pagination;

namespace LayeredTemplate.App.Features.TodoLists;

public static partial class SearchTodoLists
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
}
