using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListCreateRequestBody
{
    [Required]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}