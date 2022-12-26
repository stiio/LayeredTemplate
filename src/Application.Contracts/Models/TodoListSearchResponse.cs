using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// TodoListSearchResponse
/// </summary>
public class TodoListSearchResponse
{
    /// <summary>
    /// Pagination
    /// </summary>
    [Required]
    public PaginationResponse Pagination { get; set; } = null!;

    /// <summary>
    /// Sorting
    /// </summary>
    [Required]
    public Sorting<TodoListRecordDto> Sorting { get; set; } = null!;

    /// <summary>
    /// Filter
    /// </summary>
    public TodoListRecordDtoFilter? Filter { get; set; }

    /// <summary>
    /// Data
    /// </summary>
    [Required]
    public TodoListRecordDto[] Data { get; set; } = null!;
}