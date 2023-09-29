using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

public class TodoListGetRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}