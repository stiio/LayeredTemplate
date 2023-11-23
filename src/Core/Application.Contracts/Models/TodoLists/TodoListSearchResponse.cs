using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.Common;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

public class TodoListSearchResponse
{
    [Required]
    public PaginationResponse Pagination { get; set; } = null!;

    [Required]
    public Sorting<TodoListRecordDto> Sorting { get; set; } = null!;

    public TodoListRecordDtoFilter? Filter { get; set; }

    [Required]
    public TodoListRecordDto[] Data { get; set; } = null!;
}