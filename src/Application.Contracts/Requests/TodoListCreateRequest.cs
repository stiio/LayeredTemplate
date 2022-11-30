using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Enums;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// TodoListCreateRequest
/// </summary>
public class TodoListCreateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Name of TodoList
    /// </summary>
    /// <example>Example Name</example>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Type of TodoList
    /// </summary>
    [Required]
    public TodoListType Type { get; set; }
}