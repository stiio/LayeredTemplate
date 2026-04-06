using LayeredTemplate.App.Application.Features.TodoLists.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListGetRequest : IRequest<TodoListDto>
{
    [FromRoute]
    public Guid Id { get; set; }
}