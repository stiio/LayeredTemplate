using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListSearchRequestBody
{
    public TodoListSearchFilterDto? Filter { get; set; }

    public Sorting<TodoListFields> Sorting { get; set; } = new Sorting<TodoListFields>()
    {
        Column = TodoListFields.CreatedAt,
        Direction = DirectionType.Desc,
    };

    public PaginationRequest Pagination { get; set; } = new();
}