using LayeredTemplate.Application.Contracts.Models.Common;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

/// <summary>
/// TodoList Search Request Body
/// </summary>
public class TodoListSearchRequestBody
{
    /// <summary>
    /// Pagination
    /// </summary>
    public PaginationRequest Pagination { get; set; } = new();

    /// <summary>
    /// Sorting
    /// </summary>
    public Sorting<TodoListRecordDto> Sorting { get; set; } = new();

    /// <summary>
    /// Filter
    /// </summary>
    public TodoListRecordDtoFilter? Filter { get; set; }
}