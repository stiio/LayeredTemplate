using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// SearchTodoListFilter
/// </summary>
public class SearchTodoListFilter
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