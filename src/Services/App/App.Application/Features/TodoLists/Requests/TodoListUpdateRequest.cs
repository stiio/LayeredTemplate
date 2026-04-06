using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListUpdateRequest : IRequest<TodoListDto>
{
    [FromRoute]
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public TodoListUpdateRequestBody Body { get; set; } = null!;
}