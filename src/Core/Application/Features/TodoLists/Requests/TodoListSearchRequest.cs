using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Features.TodoLists.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Features.TodoLists.Requests;

public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    [Required]
    [FromBody]
    public TodoListSearchRequestBody Body { get; set; } = null!;
}