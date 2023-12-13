using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Features.TodoLists.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Features.TodoLists.Requests;

public class TodoListCreateRequest : IRequest<TodoListDto>
{
    [FromBody]
    [Required]
    public TodoListCreateRequestBody Body { get; set; } = null!;
}