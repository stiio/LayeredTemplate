using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Application.Features.TodoLists.Requests;

public class TodoListDeleteRequest : IRequest
{
    [FromRoute]
    public Guid Id { get; set; }
}