using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListFileCreateRequest : IRequest<TodoListDto>
{
    [Required]
    [FromJson]
    public TodoListCreateRequestBody Body { get; set; } = null!;

    [Required]
    [FromForm]
    public IFormFile File { get; set; } = null!;
}