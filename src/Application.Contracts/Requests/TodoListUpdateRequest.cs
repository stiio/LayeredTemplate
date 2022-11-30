using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Enums;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// UpdateTodoListRequest
/// </summary>
public class TodoListUpdateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Name of TodoList
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Type of TodoList
    /// </summary>
    [Required]
    public TodoListType Type { get; set; }
}