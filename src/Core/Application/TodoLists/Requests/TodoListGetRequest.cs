using LayeredTemplate.Application.TodoLists.Models;
using MediatR;

namespace LayeredTemplate.Application.TodoLists.Requests;

public class TodoListGetRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}