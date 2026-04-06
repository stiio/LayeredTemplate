using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListSearchResponse
{
    public TodoListSearchFilterDto? Filter { get; set; }

    public Sorting<TodoListDto> Sorting { get; set; } = new Sorting<TodoListDto>()
    {
        Column = nameof(TodoListDto.CreatedAt),
        Direction = DirectionType.Desc,
    };

    public PaginationResponse Pagination { get; set; } = null!;

    public TodoListDto[] Data { get; set; } = null!;
}