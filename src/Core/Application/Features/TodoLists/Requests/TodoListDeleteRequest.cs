using MediatR;

namespace LayeredTemplate.Application.Features.TodoLists.Requests;

public class TodoListDeleteRequest : IRequest
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }
}