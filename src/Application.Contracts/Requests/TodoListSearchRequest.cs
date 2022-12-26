using LayeredTemplate.Application.Contracts.Models;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// Search TodoList Request
/// </summary>
public class TodoListSearchRequest : IRequest<TodoListSearchResponse>
{
    /// <summary>
    /// Pagination
    /// </summary>
    public PaginationRequest Pagination { get; set; } = new();

    /// <summary>
    /// Sorting
    /// </summary>
    public Sorting<TodoListRecordDto> Sorting { get; set; } = new();

    /// <summary>
    /// Filter
    /// </summary>
    public TodoListRecordDtoFilter? Filter { get; set; }
}