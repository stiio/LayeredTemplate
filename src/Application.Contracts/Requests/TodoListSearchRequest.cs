using LayeredTemplate.Application.Contracts.Models;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// Search TodoList Request
/// </summary>
public class TodoListSearchRequest : IRequest<PagedList<TodoListRecordDto>>
{
    /// <summary>
    /// Pagination
    /// </summary>
    public Pagination? Pagination { get; set; }

    /// <summary>
    /// Filter
    /// </summary>
    public SearchTodoListFilter? Filter { get; set; }

    /// <summary>
    /// Sorting
    /// </summary>
    public Sorting? Sorting { get; set; }
}