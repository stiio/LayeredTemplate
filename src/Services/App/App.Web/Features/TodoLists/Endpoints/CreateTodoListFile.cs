using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;
using LayeredTemplate.Plugins.JsonMultipart;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

[EndpointGroup<TodoListsGroup>]
public sealed class CreateTodoListFile : IEndpoint
{
    /// <summary>
    /// JSON-bound multipart part. The marker interface <see cref="IJsonMultipartPart{TSelf}"/>
    /// contributes the static <c>BindAsync</c> that reads the form field of the same name as the
    /// consuming parameter (here: <c>body</c>) and deserialises it via <c>System.Text.Json</c>.
    /// </summary>
    public sealed class Body : IJsonMultipartPart<Body>
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
    }

    public sealed class Request
    {
        [FromQuery]
        public string? Q { get; set; }

        [Required]
        public Body Body { get; set; } = null!;

        [Required]
        public IFormFile File { get; set; } = null!;

        [FromForm, Required]
        public bool IsDraft { get; set; }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/file", Handle)
            .WithName(nameof(CreateTodoListFile))
            .WithSummary("Create TodoList from file (multipart + JSON example)")
            .RequireAuthorization()
            .DisableAntiforgery();

    // Parameters are bound individually: `body` via Body.BindAsync (the marker interface),
    // `file` natively (IFormFile auto-bound from multipart), `isDraft` via [FromForm].
    // No wrapping Request DTO — each binding source is explicit at the call site.
    public static TodoListDto Handle(
        [AsParameters] Request request) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = request.Body.Name,
            Description = request.Body.Description,
            CreatedAt = DateTime.UtcNow,
        };
}
