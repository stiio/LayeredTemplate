using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    [Required]
    [FromBody]
    public TodoListSearchRequestBody Body { get; set; } = null!;
}