using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListCreateRequest : IRequest<TodoListDto>
{
    [FromBody]
    [Required]
    public TodoListCreateRequestBody Body { get; set; } = null!;
}