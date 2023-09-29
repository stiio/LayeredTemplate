using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

public class TodoListRecordDtoFilter
{
    /// <summary>
    /// Search filter
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Types filter
    /// </summary>
    public TodoListType[]? Types { get; set; }
}