using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

public class TodoListCreateRequest : IRequest<TodoListDto>
{
    [FromBody]
    [Required]
    public TodoListCreateRequestBody Body { get; set; } = null!;
}