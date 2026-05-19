using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;
using LayeredTemplate.App.Shared.Validation;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

[EndpointGroup<TodoListsGroup>]
public sealed class UpdateTodoList : IEndpoint
{
    public sealed class Request
    {
        [FromRoute]
        public Guid Id { get; set; }

        [Required, FromBody]
        public Body Body { get; set; } = null!;
    }

    public sealed class Body
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/{id:guid}", Handle)
            .WithName(nameof(UpdateTodoList))
            .WithSummary("Update TodoList");

    public static TodoListDto Handle([AsParameters] Request request) =>
        new()
        {
            Id = request.Id,
            Name = request.Body.Name,
            Description = request.Body.Description,
            CreatedAt = DateTime.UtcNow,
        };
}
