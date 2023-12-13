using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Features.TodoLists.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Features.TodoLists.Requests;

public class TodoListUpdateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public TodoListUpdateRequestBody Body { get; set; } = null!;
}