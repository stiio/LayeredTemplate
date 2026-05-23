using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Features.TodoLists.Models;
using LayeredTemplate.App.Shared.Endpoints;
using LayeredTemplate.App.Shared.Errors.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.TodoLists.Endpoints;

[EndpointGroup<TodoListsGroup>]
public sealed class CreateTodoList : IEndpoint
{
    /// <summary>Request body for creating a TodoList.</summary>
    /// <example>{ "name": "some name", "description": "some description" }</example>
    public sealed class Request
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/", Handle)
            .WithName(nameof(CreateTodoList))
            .WithSummary("Create TodoList");

    public static Task<TodoListDto> Handle([FromBody] Request request)
    {
        // Stub preserved from old handler — original threw a message exception.
        throw new AppMessageException("Some message");
    }
}
