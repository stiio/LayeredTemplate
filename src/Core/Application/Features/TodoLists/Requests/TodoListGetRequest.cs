using LayeredTemplate.Application.Features.TodoLists.Models;
using MediatR;

namespace LayeredTemplate.Application.Features.TodoLists.Requests;

public class TodoListGetRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}