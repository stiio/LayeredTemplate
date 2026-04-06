namespace LayeredTemplate.App.Application.Features.TodoLists.Models;

public class TodoListDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}