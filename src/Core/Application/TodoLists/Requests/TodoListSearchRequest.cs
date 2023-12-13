using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.TodoLists.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.TodoLists.Requests;

public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    [Required]
    [FromBody]
    public TodoListSearchRequestBody Body { get; set; } = null!;
}