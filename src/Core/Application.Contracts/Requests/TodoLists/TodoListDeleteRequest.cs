using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

public class TodoListDeleteRequest : IRequest
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}