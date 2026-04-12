using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListSearchResponse
{
    public TodoListSearchFilterDto? Filter { get; set; }

    public Sorting<TodoListFields> Sorting { get; set; } = null!;

    public PaginationResponse Pagination { get; set; } = null!;

    public TodoListDto[] Data { get; set; } = null!;
}