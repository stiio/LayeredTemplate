using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// Delete TodoList Request
/// </summary>
public class TodoListDeleteRequest : IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoListDeleteRequest"/> class.
    /// </summary>
    /// <param name="id">Id of TodoList</param>
    public TodoListDeleteRequest(Guid id)
    {
        this.Id = id;
    }

    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}