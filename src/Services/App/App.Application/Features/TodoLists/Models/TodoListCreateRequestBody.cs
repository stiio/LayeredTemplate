using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

/// <summary>
/// TodoListCreateRequestBody
/// </summary>
/// <example>{ "name": "some name", "description": "some description" }</example>
public class TodoListCreateRequestBody
{
    [Required]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}