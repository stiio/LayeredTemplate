using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// UpdateTodoListRequest
/// </summary>
public class TodoListUpdateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Body
    /// </summary>
    [Required]
    [FromBody]
    public TodoListUpdateRequestBody Body { get; set; } = null!;
}