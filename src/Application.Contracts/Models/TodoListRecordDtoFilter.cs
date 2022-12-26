using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// TodoListRecordDtoFilter
/// </summary>
public class TodoListRecordDtoFilter
{
    /// <summary>
    /// Search
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Types filter
    /// </summary>
    public TodoListType[]? Types { get; set; }
}