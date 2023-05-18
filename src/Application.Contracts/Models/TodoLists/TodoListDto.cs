using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

/// <summary>
/// TodoListDto
/// </summary>
public class TodoListDto
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of TodoList
    /// </summary>
    /// <example>Example Name</example>
    public string? Name { get; set; }

    /// <summary>
    /// Type of TodoList
    /// </summary>
    public TodoListType Type { get; set; }
}