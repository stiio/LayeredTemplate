using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Validation;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

public static class UpdateTodoList
{
    public sealed class Request
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", Handle)
            .WithName(nameof(UpdateTodoList))
            .WithSummary("Update TodoList")
            .WithValidation<Request>();

    public static TodoListDto Handle(Guid id, [FromBody] Request request) =>
        new()
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
        };
}
