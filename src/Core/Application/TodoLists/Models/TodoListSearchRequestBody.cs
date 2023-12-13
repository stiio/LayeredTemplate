using LayeredTemplate.Application.Common.Models;

namespace LayeredTemplate.Application.TodoLists.Models;

public class TodoListSearchRequestBody
{
    public PaginationRequest Pagination { get; set; } = new();

    public Sorting<TodoListRecordDto> Sorting { get; set; } = new();

    public TodoListRecordDtoFilter? Filter { get; set; }
}