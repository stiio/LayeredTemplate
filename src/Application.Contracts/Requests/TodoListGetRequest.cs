using LayeredTemplate.Application.Contracts.Models;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// Get TodoList Request
/// </summary>
public class TodoListGetRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}