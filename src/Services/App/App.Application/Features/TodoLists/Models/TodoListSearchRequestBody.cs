using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListSearchRequestBody
{
    public TodoListSearchFilterDto? Filter { get; set; }

    public Sorting<TodoListDto> Sorting { get; set; } = new Sorting<TodoListDto>()
    {
        Column = nameof(TodoListDto.CreatedAt),
        Direction = DirectionType.Desc,
    };

    public PaginationRequest Pagination { get; set; } = new();
}