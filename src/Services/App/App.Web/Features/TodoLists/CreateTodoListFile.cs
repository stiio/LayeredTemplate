using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.Plugins.JsonMultipart;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists;

public static class CreateTodoListFile
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

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/file", Handle)
            .WithName(nameof(CreateTodoListFile))
            .WithSummary("Create TodoList from file (multipart + JSON example)")
            .DisableAntiforgery();

    // Parameters are bound individually: `body` via Body.BindAsync (the marker interface),
    // `file` natively (IFormFile auto-bound from multipart), `isDraft` via [FromForm].
    // No wrapping Request DTO — each binding source is explicit at the call site.
    public static TodoListDto Handle(
        Body body,
        IFormFile file,
        [FromForm] bool isDraft) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = body.Name,
            Description = body.Description,
            CreatedAt = DateTime.UtcNow,
        };
}
