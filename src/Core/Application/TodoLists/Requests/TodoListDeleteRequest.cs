using MediatR;

namespace LayeredTemplate.Application.TodoLists.Requests;

public class TodoListDeleteRequest : IRequest
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}