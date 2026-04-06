using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    [Required]
    [FromBody]
    public TodoListSearchRequestBody Body { get; set; } = null!;
}