using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests;

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