using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

/// <summary>
/// Delete TodoList Request
/// </summary>
public class TodoListDeleteRequest : IRequest
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}