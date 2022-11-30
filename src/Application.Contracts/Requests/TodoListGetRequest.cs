using LayeredTemplate.Application.Contracts.Models;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// Get TodoList Request
/// </summary>
public class TodoListGetRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoListGetRequest"/> class.
    /// </summary>
    /// <param name="id">Id of TodoList</param>
    public TodoListGetRequest(Guid id)
    {
        this.Id = id;
    }

    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}