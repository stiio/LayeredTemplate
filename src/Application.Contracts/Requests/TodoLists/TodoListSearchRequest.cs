using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

/// <summary>
/// Search TodoList Request
/// </summary>
public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    /// <summary>
    /// Body
    /// </summary>
    [Required]
    [FromBody]
    public TodoListSearchRequestBody Body { get; set; } = null!;
}