using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.Plugins.JsonMultipart;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;

namespace LayeredTemplate.App.Features.TodoLists;

public static class CreateTodoListFile
{
    public sealed class Body
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
    }

    /// <summary>
    /// Demonstrates the multipart-with-JSON pattern. <see cref="IJsonMultipartRequest{TSelf}"/>
    /// wires up <c>BindAsync</c> (custom Minimal API binding) and OpenAPI metadata so the request
    /// is described as <c>multipart/form-data</c> with the <see cref="Body"/> part carrying its
    /// content as <c>application/json</c>.
    /// </summary>
    public sealed class Request : IJsonMultipartRequest<Request>
    {
        [Required]
        [FromJson]
        public Body Body { get; set; } = null!;

        [Required]
        public IFormFile File { get; set; } = null!;
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/file", Handle)
            .WithName(nameof(CreateTodoListFile))
            .WithSummary("Create TodoList from file (multipart + JSON example)")
            .DisableAntiforgery();

    public static TodoListDto Handle(Request request) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = request.Body.Name,
            Description = request.Body.Description,
            CreatedAt = DateTime.UtcNow,
        };
}
