using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListUpdateRequestBody
{
    [Required]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}