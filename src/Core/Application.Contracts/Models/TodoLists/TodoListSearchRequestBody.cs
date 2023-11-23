using LayeredTemplate.Application.Contracts.Models.Common;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

public class TodoListSearchRequestBody
{
    public PaginationRequest Pagination { get; set; } = new();

    public Sorting<TodoListRecordDto> Sorting { get; set; } = new();

    public TodoListRecordDtoFilter? Filter { get; set; }
}