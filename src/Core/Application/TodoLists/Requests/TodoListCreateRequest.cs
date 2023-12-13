using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.TodoLists.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.TodoLists.Requests;

public class TodoListCreateRequest : IRequest<TodoListDto>
{
    [FromBody]
    [Required]
    public TodoListCreateRequestBody Body { get; set; } = null!;
}