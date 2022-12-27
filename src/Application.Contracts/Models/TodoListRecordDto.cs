using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// TodoList Record
/// </summary>
public class TodoListRecordDto
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Id of User
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User
    /// </summary>
    public UserShortInfo User { get; set; } = null!;

    /// <summary>
    /// Name of TodoList
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Type of TodoList
    /// </summary>
    public TodoListType Type { get; set; }

    /// <summary>
    /// Created At
    /// </summary>
    public DateTime CreatedAt { get; set; }
}