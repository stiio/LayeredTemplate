using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListItemsCreateRequest : IRequest<TodoListItemBase[]>
{
    [Required]
    [FromBody]
    public TodoListItemBase[] Body { get; set; } = null!;
}